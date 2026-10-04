const copilotLogins = new Set(['copilot', 'copilot-swe-agent', 'copilot-swe-agent[bot]']);

module.exports = function isCopilotLogin(login) {
  return typeof login === 'string' && copilotLogins.has(login.toLowerCase());
};
