const test = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const startWork = require('./squad-start-work.cjs');
const enableAutoMerge = require('./squad-auto-merge.cjs');
const isCopilotLogin = require('./copilot-identity.cjs');
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;

test('the identity check rejects missing, unrelated and lookalike logins', () => {
  for (const login of [undefined, null, 123, '', 'KaptenJon', 'copilot-helper', 'copilot-swe-agent-human']) {
    assert.equal(isCopilotLogin(login), false);
  }
});

function kickoffFixture(overrides = {}, assignmentLogin = 'Copilot') {
  const calls = [];
  const issue = {
    number: 111, state: 'open', labels: [{ name: 'squad' }, { name: 'squad:maja' }],
    assignees: [], user: { login: 'KaptenJon' }, ...overrides
  };
  const comments = [];
  const github = {
    rest: {
      issues: {
        get: async () => ({ data: issue }),
        listComments: () => {},
        createComment: async input => calls.push(['comment', input]),
        updateComment: async input => calls.push(['update', input])
      },
      repos: {
        getCollaboratorPermissionLevel: async () => ({ data: { permission: 'admin' } }),
        get: async () => ({ data: { default_branch: 'master' } })
      }
    },
    paginate: async () => comments
  };
  return {
    github, issue, calls, comments,
    fetchImpl: async (url, input) => {
      calls.push(['assign', JSON.parse(input.body), input, url]);
      issue.assignees = [{ login: assignmentLogin }];
      return { ok: true, json: async () => issue };
    },
    context: { repo: { owner: 'KaptenJon', repo: 'HKA_Handball' }, actor: 'KaptenJon' },
    core: { info() {} },
    env: { ISSUE_NUMBER: '111', COPILOT_ASSIGN_TOKEN: 'test-token' }
  };
}

test('assigns the specialist-owned issue to the compact Squad cloud agent', async () => {
  const f = kickoffFixture();
  await startWork(f);
  const request = f.calls.find(([kind]) => kind === 'assign')[1];
  assert.deepEqual(request.assignees, ['copilot-swe-agent[bot]']);
  assert.equal(request.agent_assignment.custom_agent, 'squad-cloud');
  assert.equal(request.agent_assignment.base_branch, 'master');
  assert.equal(request.agent_assignment.target_repo, 'KaptenJon/HKA_Handball');
  assert.equal(f.calls[0][2].headers.authorization, 'Bearer test-token');
  assert.equal(f.calls[0][2].method, 'POST');
  assert.equal(f.calls[0][3], 'https://api.github.com/repos/KaptenJon/HKA_Handball/issues/111/assignees');
  assert.match(request.agent_assignment.custom_instructions, /Closes #111/);
  assert.match(f.calls.find(([kind]) => kind === 'comment')[1].body, /Squad started/);
  assert.equal(f.issue.labels[1].name, 'squad:maja');
});

test('the actual cloud profile fits GitHub limits and requires real delegated review', () => {
  const profile = readFileSync('.github/agents/squad-cloud.agent.md', 'utf8');
  assert.ok(profile.length <= 30000);
  assert.match(profile, /"agent"/);
  assert.match(profile, /Invoke PR Reviewer/);
  assert.match(profile, /Human review is mandatory/);
  assert.match(profile, /specialist invocation is unavailable/);
});

for (const login of ['Copilot', 'copilot', 'copilot-swe-agent', 'copilot-swe-agent[bot]']) {
  test(`the assignment response recognizes ${login} as successful execution`, async () => {
    const f = kickoffFixture({}, login);
    await startWork(f);
    assert.equal(f.calls.filter(([kind]) => kind === 'assign').length, 1);
    assert.match(f.calls.find(([kind]) => kind === 'comment')[1].body, /Squad started/);
  });

  test(`an existing ${login} assignee prevents duplicate sessions`, async () => {
    const f = kickoffFixture({ assignees: [{ login }] });
    await startWork(f);
    assert.equal(f.calls.length, 0);
  });
}

test('an existing Copilot assignment repairs the old false failure comment without a new session', async () => {
  const f = kickoffFixture({ assignees: [{ login: 'Copilot' }] });
  f.comments.push({
    id: 2, user: { login: 'github-actions[bot]' },
    body: '<!-- squad-execution-status -->\n**Squad could not start:** GitHub did not assign Copilot.'
  });
  await startWork(f);
  assert.deepEqual(f.calls.map(([kind]) => kind), ['update']);
  assert.equal(f.calls[0][1].comment_id, 2);
  assert.match(f.calls[0][1].body, /Squad running/);
});

for (const label of ['go:no', 'go:blocked', 'go:needs-human']) {
  test(`${label} blocks execution with a visible comment`, async () => {
    const f = kickoffFixture({ labels: [{ name: 'squad' }, { name: label }] });
    await startWork(f);
    assert.ok(!f.calls.some(([kind]) => kind === 'assign'));
    assert.match(f.calls[0][1].body, /Squad paused/);
  });
}

for (const overrides of [{ state: 'closed' }, { pull_request: { url: 'example' } }]) {
  test(`does not assign ${overrides.state || 'pull request'} items`, async () => {
    const f = kickoffFixture(overrides);
    await startWork(f);
    assert.equal(f.calls.length, 0);
  });
}

test('missing credentials fail explicitly instead of claiming execution started', async () => {
  const f = kickoffFixture();
  delete f.env.COPILOT_ASSIGN_TOKEN;
  await assert.rejects(startWork(f), /COPILOT_ASSIGN_TOKEN/);
  assert.match(f.calls[0][1].body, /Squad could not start/);
  assert.ok(!f.calls.some(([kind]) => kind === 'assign'));
});

test('the API must actually return a Copilot assignee', async () => {
  const f = kickoffFixture();
  f.fetchImpl = async () => ({ ok: true, json: async () => ({ assignees: [] }) });
  await assert.rejects(startWork(f), /did not assign Copilot/);
  assert.match(f.calls[0][1].body, /Squad could not start/);
});

test('assignment API failures update a single bot-owned status comment', async () => {
  const f = kickoffFixture();
  f.comments.push(
    { id: 1, user: { login: 'someone' }, body: '<!-- squad-execution-status -->' },
    { id: 2, user: { login: 'github-actions[bot]' }, body: '<!-- squad-execution-status -->' }
  );
  f.fetchImpl = async () => { throw new Error('Assignment denied'); };
  await assert.rejects(startWork(f), /Assignment denied/);
  assert.equal(f.calls[0][0], 'update');
  assert.equal(f.calls[0][1].comment_id, 2);
  assert.match(f.calls[0][1].body, /Assignment denied/);
});

test('unknown public contributors cannot start paid execution', async () => {
  const f = kickoffFixture();
  f.github.rest.repos.getCollaboratorPermissionLevel = async () => {
    throw Object.assign(new Error('Not Found'), { status: 404 });
  };
  await startWork(f);
  assert.match(f.calls[0][1].body, /maintainer must add/);
  assert.ok(!f.calls.some(([kind]) => kind === 'assign'));
});

test('permission API errors are not mistaken for untrusted-user verdicts', async () => {
  const f = kickoffFixture();
  f.github.rest.repos.getCollaboratorPermissionLevel = async () => {
    throw Object.assign(new Error('Rate limited'), { status: 403 });
  };
  await assert.rejects(startWork(f), /Rate limited/);
  assert.match(f.calls[0][1].body, /Squad could not start/);
});

test('a rejected assignment HTTP response is surfaced on the issue', async () => {
  const f = kickoffFixture();
  f.fetchImpl = async () => ({
    ok: false, status: 403, json: async () => ({ message: 'User token denied' })
  });
  await assert.rejects(startWork(f), /HTTP 403.*User token denied/);
  assert.match(f.calls[0][1].body, /Squad could not start/);
});

test('workflow-created weekly issues can start without collaborator lookup', async () => {
  const f = kickoffFixture({
    user: { login: 'github-actions[bot]' },
    labels: ['squad', 'weekly-improvement', 'auto-agent-candidate']
  });
  f.context.actor = 'github-actions[bot]';
  f.github.rest.repos.getCollaboratorPermissionLevel = async () => {
    throw new Error('Bot permission lookup should not be called');
  };
  await startWork(f);
  assert.ok(f.calls.some(([kind]) => kind === 'assign'));
});

test('heartbeat dispatch can retry maintainer-authored issues', async () => {
  const f = kickoffFixture();
  f.context.actor = 'github-actions[bot]';
  f.github.rest.repos.getCollaboratorPermissionLevel = async input => {
    assert.equal(input.username, 'KaptenJon');
    return { data: { permission: 'admin' } };
  };
  await startWork(f);
  assert.ok(f.calls.some(([kind]) => kind === 'assign'));
});

test('invalid issue numbers do not reach the API', async () => {
  const f = kickoffFixture();
  f.env.ISSUE_NUMBER = '111; arbitrary-input';
  await assert.rejects(startWork(f), /positive integer/);
  assert.equal(f.calls.length, 0);
});

function mergeFixture() {
  const calls = [];
  const repository = {
    full_name: 'KaptenJon/HKA_Handball', default_branch: 'master', allow_auto_merge: true
  };
  const pr = {
    number: 112, state: 'open', draft: false,
    head: { ref: 'copilot/fix-ui', repo: { full_name: repository.full_name } },
    base: { ref: 'master' }, user: { login: 'copilot-swe-agent[bot]' }
  };
  const graph = {
    id: 'PR_112', autoMergeRequest: null,
    closingIssuesReferences: { nodes: [{
      repository: { nameWithOwner: repository.full_name },
      labels: { nodes: [{ name: 'squad' }] }
    }] }
  };
  const rules = [
    { type: 'pull_request', parameters: {
      required_approving_review_count: 1, dismiss_stale_reviews_on_push: true
    } },
    { type: 'required_status_checks', parameters: {
      required_status_checks: [{ context: 'build-android' }, { context: 'automation-contracts' }]
    } }
  ];
  return {
    calls, pr, graph, rules, repository,
    github: {
      rest: {
        pulls: { get: async () => ({ data: pr }) },
        repos: { get: async () => ({ data: repository }), getBranchRules() {} }
      },
      paginate: async () => rules,
      graphql: async (query, variables) => {
        calls.push({ query, variables });
        return { repository: { pullRequest: graph } };
      }
    },
    context: {
      repo: { owner: 'KaptenJon', repo: 'HKA_Handball' },
      payload: { pull_request: { number: 112 } }
    },
    core: { info() {} }
  };
}

test('enables native squash auto-merge rather than directly merging', async () => {
  const f = mergeFixture();
  await enableAutoMerge(f);
  assert.equal(f.calls.length, 2);
  assert.match(f.calls[1].query, /enablePullRequestAutoMerge/);
  assert.match(f.calls[1].query, /mergeMethod: SQUASH/);
  assert.deepEqual(f.calls[1].variables, { id: 'PR_112' });
});

for (const login of ['Copilot', 'copilot', 'copilot-swe-agent', 'copilot-swe-agent[bot]']) {
  test(`auto-merge recognizes the Copilot author alias ${login}`, async () => {
    const f = mergeFixture();
    f.pr.user.login = login;
    await enableAutoMerge(f);
    assert.equal(f.calls.length, 2);
    assert.match(f.calls[1].query, /enablePullRequestAutoMerge/);
  });
}

for (const [name, change] of [
  ['draft', f => { f.pr.draft = true; }],
  ['closed', f => { f.pr.state = 'closed'; }],
  ['fork', f => { f.pr.head.repo.full_name = 'someone/fork'; }],
  ['wrong base', f => { f.pr.base.ref = 'other'; }],
  ['human author', f => { f.pr.user.login = 'someone'; }],
  ['unrelated branch', f => { f.pr.head.ref = 'feature/ui'; }]
]) {
  test(`does not enable auto-merge for ${name} PRs`, async () => {
    const f = mergeFixture();
    change(f);
    await enableAutoMerge(f);
    assert.equal(f.calls.length, 0);
  });
}

test('an issue in another repository cannot authorize auto-merge', async () => {
  const f = mergeFixture();
  f.graph.closingIssuesReferences.nodes[0].repository.nameWithOwner = 'someone/other';
  await enableAutoMerge(f);
  assert.equal(f.calls.length, 1);
});

test('a linked issue without the squad label cannot authorize auto-merge', async () => {
  const f = mergeFixture();
  f.graph.closingIssuesReferences.nodes[0].labels.nodes = [{ name: 'bug' }];
  await enableAutoMerge(f);
  assert.equal(f.calls.length, 1);
});

test('auto-merge enabling is idempotent', async () => {
  const f = mergeFixture();
  f.graph.autoMergeRequest = { enabledAt: '2026-10-04' };
  await enableAutoMerge(f);
  assert.equal(f.calls.length, 1);
});

for (const [name, change] of [
  ['missing approval rule', f => { f.rules.shift(); }],
  ['zero approvals', f => { f.rules[0].parameters.required_approving_review_count = 0; }],
  ['stale approvals retained', f => { f.rules[0].parameters.dismiss_stale_reviews_on_push = false; }],
  ['missing build check', f => { f.rules[1].parameters.required_status_checks = []; }],
  ['missing automation check', f => {
    f.rules[1].parameters.required_status_checks = [{ context: 'build-android' }];
  }],
  ['disabled auto-merge', f => { f.repository.allow_auto_merge = false; }]
]) {
  test(`fails closed with ${name}`, async () => {
    const f = mergeFixture();
    change(f);
    await assert.rejects(enableAutoMerge(f), /auto-merge|Auto-merge/);
    assert.equal(f.calls.length, 1);
  });
}

function workflowScript(path) {
  const source = readFileSync(path, 'utf8').replace(/\r\n/g, '\n');
  const scripts = source.split('          script: |\n');
  assert.ok(scripts.length > 1, `No script block in ${path}`);
  const script = scripts.at(-1);
  assert.ok(script, `No script in ${path}`);
  return new AsyncFunction('github', 'context', 'core', 'require',
    script.replace(/^            /gm, ''));
}

test('actual reusable triage keeps the existing specialist and avoids duplicate comments', async () => {
  const f = kickoffFixture();
  const script = workflowScript('.github/workflows/squad-triage.yml');
  process.env.ISSUE_NUMBER = '111';
  await script(f.github, f.context, f.core, require);
  assert.equal(f.calls.length, 0);
});

test('actual triage routes a weekly UI improvement to maja without overriding a blocker', async () => {
  const f = kickoffFixture({
    title: 'Weekly Improvement 2026-W40', body: 'Visual polish. Improve UI on Android.',
    labels: [{ name: 'squad' }, { name: 'go:blocked' }]
  });
  f.github.rest.issues.addLabels = async input => f.calls.push(['labels', input]);
  process.env.ISSUE_NUMBER = '111';
  await workflowScript('.github/workflows/squad-triage.yml')(
    f.github, f.context, f.core, require);
  const labels = f.calls.filter(([kind]) => kind === 'labels').flatMap(([, input]) => input.labels);
  assert.deepEqual(labels, ['squad:maja']);
  assert.match(f.calls.find(([kind]) => kind === 'comment')[1].body, /Squad Triage/);
});

for (const login of ['Copilot', 'copilot', 'copilot-swe-agent', 'copilot-swe-agent[bot]']) {
  test(`the actual heartbeat skips ${login} assignments but resumes unassigned issues`, async () => {
    const { createRequire } = require('node:module');
    const { resolve } = require('node:path');
    const calls = [];
    const github = {
      rest: { issues: { listForRepo() {} }, actions: {
        createWorkflowDispatch: async input => calls.push(input)
      } },
      paginate: async () => [
        { number: 114, assignees: [{ login }], labels: [{ name: 'squad' }] },
        { number: 111, assignees: [], labels: [{ name: 'squad' }] }
      ]
    };
    const context = {
      repo: { owner: 'KaptenJon', repo: 'HKA_Handball' },
      payload: { repository: { default_branch: 'master' } }
    };
    await workflowScript('.github/workflows/squad-heartbeat.yml')(
      github, context, { info() {} }, createRequire(resolve('workflow-script.cjs')));
    assert.deepEqual(calls.map(call => call.inputs.issue_number), ['111']);
    assert.equal(calls[0].workflow_id, 'issue-agent-pipeline.yml');
  });
}

test('production entry points call execution directly and templates stay synchronized', () => {
  const pipeline = readFileSync('.github/workflows/issue-agent-pipeline.yml', 'utf8');
  assert.match(pipeline, /types: \[opened\]/);
  assert.match(pipeline, /workflow_dispatch:/);
  assert.match(pipeline, /uses: \.\/\.github\/workflows\/squad-triage.yml/);
  assert.match(pipeline, /needs: triage[\s\S]*uses: \.\/\.github\/workflows\/squad-start-work.yml/);
  assert.doesNotMatch(pipeline, /sleep|pulls\.merge/);
  for (const name of ['squad-triage', 'squad-issue-assign', 'squad-heartbeat']) {
    assert.equal(readFileSync(`.github/workflows/${name}.yml`, 'utf8'),
      readFileSync(`.squad/templates/workflows/${name}.yml`, 'utf8'), name);
  }
  const labels = readFileSync('.github/workflows/squad-issue-assign.yml', 'utf8');
  assert.match(labels, /uses: \.\/\.github\/workflows\/issue-agent-pipeline.yml/);
  const heartbeat = readFileSync('.github/workflows/squad-heartbeat.yml', 'utf8');
  assert.match(heartbeat, /createWorkflowDispatch/);
  assert.match(heartbeat, /issue-agent-pipeline.yml/);
  const weekly = readFileSync('.github/workflows/weekly-improvement-issue.yml', 'utf8');
  assert.match(weekly, /await dispatchIssue\(existing\[0\].number\)/);
  assert.match(weekly, /await dispatchIssue\(created.data.number\)/);
});

test('weekly issue creation reuses the open issue and dispatches the string issue number', async () => {
  const dispatches = [];
  const queries = [];
  const existingIssue = { number: 126, title: 'Weekly Improvement this week' };
  const github = {
    rest: {
      issues: {
        listForRepo() {},
        getLabel: async () => ({}),
        createLabel: async () => {},
        create: async () => { throw new Error('Should reuse the open weekly issue'); }
      },
      actions: {
        createWorkflowDispatch: async input => dispatches.push(input)
      }
    },
    paginate: async (_method, params) => {
      queries.push(params);
      return [existingIssue];
    }
  };
  const context = { repo: { owner: 'KaptenJon', repo: 'HKA_Handball' } };

  await workflowScript('.github/workflows/weekly-improvement-issue.yml')(
    github, context, { info() {} }, require);

  assert.equal(queries.length, 1);
  assert.equal(queries[0].state, 'open');
  assert.match(queries[0].labels, /^weekly-\d{4}-W\d{2}$/);
  assert.equal(dispatches.length, 1);
  assert.equal(dispatches[0].workflow_id, 'issue-agent-pipeline.yml');
  assert.equal(dispatches[0].ref, '${{ github.ref_name }}');
  assert.equal(dispatches[0].inputs.issue_number, '126');
});

test('weekly issue prompt reviews UI and gameplay, includes recent history, and dispatches creation', async () => {
  const dispatches = [];
  const createdIssues = [];
  const historyQueries = [];
  const github = {
    rest: {
      issues: {
        listForRepo: async params => {
          historyQueries.push(params);
          return { data: [
            {
              number: 125,
              title: 'Weekly Improvement 2026-W39',
              body: 'Improve substitution feedback when players enter the match.'
            },
            {
              number: 124,
              title: 'Weekly Improvement 2026-W38',
              body: 'An old idea that should not appear in the bounded context.',
              pull_request: { url: 'https://api.github.com/repos/example/pulls/124' }
            }
          ] };
        },
        getLabel: async () => ({}),
        createLabel: async () => {},
        create: async input => {
          createdIssues.push(input);
          return { data: { number: 127 } };
        }
      },
      actions: {
        createWorkflowDispatch: async input => dispatches.push(input)
      }
    },
    paginate: async () => []
  };
  const context = { repo: { owner: 'KaptenJon', repo: 'HKA_Handball' } };

  await workflowScript('.github/workflows/weekly-improvement-issue.yml')(
    github, context, { info() {} }, require);

  assert.equal(historyQueries.length, 1);
  assert.equal(historyQueries[0].state, 'all');
  assert.equal(historyQueries[0].labels, 'weekly-improvement');
  assert.equal(historyQueries[0].per_page, 100);
  assert.equal(historyQueries[0].sort, 'updated');
  assert.equal(historyQueries[0].direction, 'desc');
  assert.equal(createdIssues.length, 1);
  const body = createdIssues[0].body;
  assert.match(body, /Review the current app and repository/);
  assert.match(body, /UI and the gameplay\/general match experience/);
  assert.match(body, /multiple distinct suggestions/);
  assert.match(body, /likely player impact and point to relevant screens, systems, or code areas/);
  assert.match(body, /Avoid generic ideas and avoid repeating or lightly rewording recent weekly improvements/);
  assert.match(body, /select and implement one small, high-value improvement/);
  assert.match(body, /"Weekly Improvement 2026-W39".*"Improve substitution feedback when players enter the match\."/);
  assert.doesNotMatch(body, /old idea that should not appear/);
  assert.match(body, /authentic handball rules and gameplay/);
  assert.match(body, /offline-first and privacy-respecting/);
  assert.equal(dispatches.length, 1);
  assert.equal(dispatches[0].workflow_id, 'issue-agent-pipeline.yml');
  assert.equal(dispatches[0].ref, '${{ github.ref_name }}');
  assert.equal(dispatches[0].inputs.issue_number, '127');
});

test('CI runs the actual automation contract assertions', () => {
  const build = readFileSync('.github/workflows/build.yml', 'utf8');
  assert.match(build, /automation-contracts:/);
  assert.match(build, /node --test \.github\/scripts\/squad-automation.test.cjs/);
});

function mutant(path, from, to) {
  const { Module } = require('node:module');
  const source = readFileSync(path, 'utf8');
  assert.ok(source.includes(from), `Mutation target not found in ${path}`);
  const compiled = new Module(path);
  compiled.filename = path;
  compiled._compile(source.replace(from, to), path);
  return compiled.exports;
}

test('contract assertions reject a real-source mutation that removes the review gate', async () => {
  const unsafe = mutant('.github/scripts/squad-auto-merge.cjs',
    'if (!requiresReview ||', 'if (false ||');
  const f = mergeFixture();
  f.rules[0].parameters.required_approving_review_count = 0;
  await assert.rejects(
    assert.rejects(unsafe(f), /Auto-merge requires/),
    { code: 'ERR_ASSERTION' }
  );
});

test('contract assertions reject a real-source mutation that allows duplicate sessions', async () => {
  const unsafe = mutant('.github/scripts/squad-start-work.cjs',
    'if (issue.assignees?.some', 'if (false && issue.assignees?.some');
  const f = kickoffFixture({ assignees: [{ login: 'copilot-swe-agent[bot]' }] });
  await unsafe(f);
  assert.throws(() => assert.equal(f.calls.length, 0), { code: 'ERR_ASSERTION' });
});
