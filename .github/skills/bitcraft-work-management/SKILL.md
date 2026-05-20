---
name: bitcraft-work-management
description: 'Manage Bitcraft human-agent work from overview to research, planning, Lead Dev delegation, expert execution, review, metrics, inbox updates, and documentation. Use for task breakdown, handoffs, roadmaps, and avoiding scope drift.'
argument-hint: '[idea, feature, task, roadmap, inbox state, or current work state]'
---

# Bitcraft Work Management

Use this skill to keep human-agent development organized and auditable.

## When to Use This Skill

- The user asks for a roadmap, plan, work breakdown, or status.
- Multiple agents, prompts, skills, or inboxes will be involved.
- The task has acceptance criteria or must be tracked across iterations.

## Procedure

1. Restate the requested outcome and acceptance criteria.
2. Define explicit non-goals and protected files.
3. Choose the workflow route: overview, research, plan, develop, review, audit, write, metrics, or specialist agent.
4. Use `.github/inboxes/<agent-slug>/inbox.md` for future cross-agent work.
5. Track only meaningful steps; avoid todo noise for tiny tasks.
6. Validate after each substantive edit before widening scope.
7. End with changed files, validation, risks, metrics impact, inbox updates, and next practical step.

## Handoff Pattern

- Secretary -> Researcher when discovery or external docs matter.
- Secretary or Researcher -> Planner when a decision needs implementation structure.
- Planner -> Lead Dev for development work.
- Lead Dev -> NET Dev, Web Dev, Dev Ops, Sec Ops, or AI Ops for expert slices.
- Lead Dev -> review route based on CIL.
- Audit agents -> inbox items for iterative remediation.

## Gotchas

- Do not let a plan become an unverified claim about current repository state.
- Do not keep planning after implementation is explicitly requested.
- Do not let inboxes become historical logs.
