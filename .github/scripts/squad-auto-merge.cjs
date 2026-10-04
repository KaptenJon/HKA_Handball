const isCopilotLogin = require('./copilot-identity.cjs');

module.exports = async function enableAutoMerge({ github, context, core }) {
  const { owner, repo } = context.repo;
  const pullNumber = context.payload.pull_request.number;
  const { data: pr } = await github.rest.pulls.get({
    owner, repo, pull_number: pullNumber
  });
  const { data: repository } = await github.rest.repos.get({ owner, repo });
  if (pr.state !== 'open' || pr.draft ||
      pr.head.repo?.full_name !== repository.full_name ||
      pr.base.ref !== repository.default_branch ||
      !pr.head.ref.startsWith('copilot/') ||
      !isCopilotLogin(pr.user.login)) {
    core.info('Not a ready, same-repository Copilot PR against the default branch.');
    return;
  }

  const result = await github.graphql(`
    query($owner: String!, $repo: String!, $number: Int!) {
      repository(owner: $owner, name: $repo) {
        pullRequest(number: $number) {
          id
          autoMergeRequest { enabledAt }
          closingIssuesReferences(first: 100) {
            nodes { repository { nameWithOwner } labels(first: 100) { nodes { name } } }
          }
        }
      }
    }`, { owner, repo, number: pullNumber });
  const pullRequest = result.repository.pullRequest;
  const squadIssue = pullRequest.closingIssuesReferences.nodes.some(issue =>
    issue.repository.nameWithOwner === repository.full_name &&
    issue.labels.nodes.some(label => label.name === 'squad'));
  if (!squadIssue) {
    core.info('No linked Squad issue; auto-merge is not enabled.');
    return;
  }
  if (pullRequest.autoMergeRequest) {
    core.info('Auto-merge is already enabled.');
    return;
  }
  if (!repository.allow_auto_merge) {
    throw new Error('Enable repository auto-merge before using Squad Reviewed Auto-Merge.');
  }

  const rules = await github.paginate(github.rest.repos.getBranchRules, {
    owner, repo, branch: repository.default_branch, per_page: 100
  });
  const requiresReview = rules.some(rule => rule.type === 'pull_request' &&
    rule.parameters.required_approving_review_count >= 1 &&
    rule.parameters.dismiss_stale_reviews_on_push);
  const requiredChecks = rules.filter(rule => rule.type === 'required_status_checks')
    .flatMap(rule => rule.parameters.required_status_checks.map(check => check.context));
  if (!requiresReview ||
      !['build-android', 'automation-contracts'].every(check => requiredChecks.includes(check))) {
    throw new Error('Auto-merge requires branch rules enforcing a human approval, stale-review dismissal, build-android and automation-contracts.');
  }

  await github.graphql(`
    mutation($id: ID!) {
      enablePullRequestAutoMerge(input: {pullRequestId: $id, mergeMethod: SQUASH}) {
        pullRequest { number }
      }
    }`, { id: pullRequest.id });
  core.info(`Auto-merge enabled for #${pullNumber}; GitHub enforces approval and required checks.`);
};
