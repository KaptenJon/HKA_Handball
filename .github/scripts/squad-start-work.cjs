const { readFileSync } = require('node:fs');
const isCopilotLogin = require('./copilot-identity.cjs');

const marker = '<!-- squad-execution-status -->';

module.exports = async function startWork({
  github, context, core, env = process.env, fetchImpl = fetch
}) {
  const issueNumber = Number(env.ISSUE_NUMBER);
  if (!Number.isSafeInteger(issueNumber) || issueNumber <= 0) {
    throw new Error('ISSUE_NUMBER must be a positive integer.');
  }

  const params = { ...context.repo, issue_number: issueNumber };
  const { data: issue } = await github.rest.issues.get(params);
  if (issue.pull_request || issue.state !== 'open') {
    core.info('Only open issues can start Squad work.');
    return;
  }

  const comments = await github.paginate(github.rest.issues.listComments, params);
  async function report(body) {
    const previous = comments.find(comment =>
      comment.user?.login === 'github-actions[bot]' && comment.body?.includes(marker));
    if (previous) {
      await github.rest.issues.updateComment({
        ...context.repo, comment_id: previous.id, body: `${marker}\n${body}`
      });
    } else {
      await github.rest.issues.createComment({ ...params, body: `${marker}\n${body}` });
    }
  }

  const labels = issue.labels.map(label => typeof label === 'string' ? label : label.name);
  if (labels.some(label => ['go:no', 'go:blocked', 'go:needs-human'].includes(label))) {
    await report('**Squad paused:** this issue has a blocking verdict. Remove the blocker and run Issue Agent Pipeline to retry.');
    return;
  }

  if (issue.assignees?.some(assignee => isCopilotLogin(assignee.login))) {
    if (comments.some(comment => comment.user?.login === 'github-actions[bot]' &&
        comment.body?.includes(marker) && comment.body.includes('**Squad could not start:**'))) {
      await report('**Squad running:** the existing Copilot assignment is confirmed. The earlier kickoff failure was incorrect; no additional session was started.');
    }
    core.info(`Copilot is already assigned to #${issueNumber}; no duplicate session started.`);
    return;
  }

  try {
    const actor = context.actor;
    const scheduledIssue = actor === 'github-actions[bot]' &&
      issue.user?.login === 'github-actions[bot]' &&
      labels.includes('weekly-improvement') && labels.includes('auto-agent-candidate');
    let trusted = false;
    if (!scheduledIssue) {
      try {
        const permissionActor = actor === 'github-actions[bot]' ? issue.user.login : actor;
        const { data: permission } = await github.rest.repos.getCollaboratorPermissionLevel({
          ...context.repo, username: permissionActor
        });
        trusted = ['admin', 'maintain', 'write'].includes(permission.permission);
      } catch (error) {
        if (error.status !== 404) throw error;
        core.info('The triggering actor is not a repository collaborator.');
      }
    }
    if (!trusted && !scheduledIssue) {
      await report('**Squad needs approval:** a maintainer must add a `squad`/`squad:*` label or manually run Issue Agent Pipeline before this public issue can start an agent.');
      return;
    }

    if (!env.COPILOT_ASSIGN_TOKEN) {
      throw new Error('Add the COPILOT_ASSIGN_TOKEN Actions secret (a user token with Copilot access), then run Issue Agent Pipeline again.');
    }
    const profile = readFileSync('.github/agents/squad-cloud.agent.md', 'utf8');
    if (profile.length > 30000) {
      throw new Error('The Squad cloud profile exceeds GitHub\'s 30,000-character custom-agent limit.');
    }
    const { data: repository } = await github.rest.repos.get(context.repo);
    const response = await fetchImpl(
      `${context.apiUrl || 'https://api.github.com'}/repos/${context.repo.owner}/${context.repo.repo}/issues/${issueNumber}/assignees`, {
        method: 'POST',
        headers: {
          authorization: `Bearer ${env.COPILOT_ASSIGN_TOKEN}`,
          accept: 'application/vnd.github+json',
          'content-type': 'application/json',
          'x-github-api-version': '2022-11-28'
        },
        body: JSON.stringify({
          assignees: ['copilot-swe-agent[bot]'],
          agent_assignment: {
            target_repo: `${context.repo.owner}/${context.repo.repo}`,
            base_branch: repository.default_branch,
            custom_agent: 'squad-cloud',
            custom_instructions: [
              `Handle issue #${issueNumber} through Squad: plan, delegate implementation, review, validate and open a draft PR.`,
              'Read the repository-local team, routing and selected specialist charter.',
              `Include Closes #${issueNumber} in the PR description.`,
              'Human review is required before merge. Report blockers explicitly.'
            ].join('\n')
          }
        })
      });
    const assigned = await response.json();
    if (!response.ok) {
      throw new Error(`Copilot assignment failed (HTTP ${response.status}): ${assigned.message || response.statusText}`);
    }
    if (!assigned.assignees?.some(assignee => isCopilotLogin(assignee.login))) {
      throw new Error('GitHub did not assign Copilot. Verify the token permissions and cloud-agent access for this repository.');
    }
    await report('**Squad started:** Copilot is assigned using the Squad Cloud coordinator. Squad will plan, implement, review and open a draft PR. A human must review it and mark it ready; auto-merge then waits for required approval and checks.');
  } catch (error) {
    await report(`**Squad could not start:** ${error.message}\n\nRetry using **Actions -> Issue Agent Pipeline -> Run workflow** with this issue number after resolving the blocker.`);
    throw error;
  }
};
