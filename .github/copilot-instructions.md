# Copilot custom instructions (workshop mode)

This repo is used for a workshop/exercise.

## Key goal
Help the developer build the solution from context and instructions, **not** by copying an existing completed implementation.

## Source of truth
- For challenge 2 you should follow the agent guidance in `.github/agents/agentplanning.agent.md`.

## Do not use solution code as a template
Unless the user explicitly asks to reveal or compare with a solution, **do not open, reference, or reuse code** from any folder intended to contain completed answers, for example:
- `example-solution/**`
- `solutions/**`
- `**/*solution*/**`
- `**/*Solution*/**`
- `**/reference/**`

If such folders exist in the workspace, treat them as off-limits.

## How to proceed instead
- Ask clarifying questions only when required.
- Implement incrementally in the exercise project.
- Prefer small, verifiable changes; keep logging helpful.
