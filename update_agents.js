const fs = require('fs');

let yaml = fs.readFileSync('.github/workflows/issue-agent-pipeline.yml', 'utf8');

yaml = yaml.replace(
  /await github\.rest\.issues\.createComment\(\{[\s\S]*?"@copilot Please use the `pr-reviewer` custom agent\.",[\s\S]*?\}\);/,
  `try {
              await github.request('POST /agents/repos/{owner}/{repo}/tasks', {
                owner: context.repo.owner,
                repo: context.repo.repo,
                prompt: "You are the pr-reviewer custom agent. Review this PR for correctness and maintainability, and leave actionable comments.",
                pull_request_number: prNumber,
                headers: {
                  'X-GitHub-Api-Version': '2026-03-10'
                }
              });
              core.info("Triggered review agent via API");
            } catch (error) {
              core.error("Failed to trigger review agent: " + error.message);
              // Fallback
              await github.rest.issues.createComment({
                owner: context.repo.owner,
                repo: context.repo.repo,
                issue_number: prNumber,
                body: [
                  "@copilot Please use the \`pr-reviewer\` custom agent.",
                  "Review this PR for correctness and maintainability, and leave actionable comments."
                ].join("\\n")
              });
            }`
);

yaml = yaml.replace(
  /await github\.rest\.issues\.createComment\(\{[\s\S]*?"@copilot Please use the `review-comment-fixer` custom agent\.",[\s\S]*?\}\);/,
  `try {
              await github.request('POST /agents/repos/{owner}/{repo}/tasks', {
                owner: context.repo.owner,
                repo: context.repo.repo,
                prompt: "You are the review-comment-fixer custom agent. Resolve open review comments and update this PR until merge-ready.",
                pull_request_number: prNumber,
                headers: {
                  'X-GitHub-Api-Version': '2026-03-10'
                }
              });
              core.info("Triggered fix-comments agent via API");
            } catch (error) {
              core.error("Failed to trigger fix-comments agent: " + error.message);
              // Fallback
              await github.rest.issues.createComment({
                owner: context.repo.owner,
                repo: context.repo.repo,
                issue_number: prNumber,
                body: [
                  "@copilot Please use the \`review-comment-fixer\` custom agent.",
                  "Resolve open review comments and update this PR until merge-ready."
                ].join("\\n")
              });
            }`
);

yaml = yaml.replace(
  /  auto-merge:[\s\S]*?merge-method: squash/m,
  `  auto-merge:
    name: Merge PR
    runs-on: ubuntu-latest
    needs: [fix-comments-agent, wait-for-pr]
    steps:
      - name: Merge PR
        uses: actions/github-script@v9
        with:
          script: |
            const prNumber = Number("\${{ needs.wait-for-pr.outputs.pr_number }}");
            if (Number.isNaN(prNumber)) {
              core.setFailed("PR number was not set by wait-for-pr job.");
              return;
            }
            try {
              await github.rest.pulls.merge({
                owner: context.repo.owner,
                repo: context.repo.repo,
                pull_number: prNumber,
                merge_method: "squash"
              });
              core.info("Successfully merged PR #" + prNumber);
            } catch (error) {
              core.setFailed("Failed to merge PR: " + error.message);
            }`
);

fs.writeFileSync('.github/workflows/issue-agent-pipeline.yml', yaml);
