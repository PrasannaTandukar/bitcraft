---
created_at: 2026-09-17
edited_at: 2026-09-17
creator: ai-ops
editor: ai-ops
ai_gen: true
name: bitcraft-ste
description: "Rewrite ambiguous or dense English into ASD-STE100-inspired Simplified Technical English for Bitcraft agent-facing text. Use when writing or reviewing tool descriptions, error messages, inter-agent instructions, inbox items, plan acceptance criteria, prompts, skills, agents, and governance rules — anywhere a weaker model or downstream agent must parse text without a human to resolve ambiguity. Not for creative or marketing copy."
argument-hint: "[text to simplify, or a file/surface to STE-review]"
---

# Bitcraft STE (Simplified Technical English)

Adapted from [danyuchn/asd-ste100-skill](https://github.com/danyuchn/asd-ste100-skill) (MIT). That skill encodes the rule categories of ASD-STE100 Issue 9 — the controlled-language standard built so aircraft maintenance instructions cannot be misread — for agent-facing English. Bitcraft uses it because ambiguity and hedge-stacked prose are the main failure mode when weaker or faster models execute our workflows.

## When to Use This Skill

- Writing or editing `.github` agentic stack files: prompts, skills, agents, instructions, inbox items, plan acceptance criteria, governance rules.
- Writing tool descriptions, error messages, inter-agent instructions, or status reports that agents parse.
- Reviewing any steering doc for ambiguity before weaker models rely on it.
- The user asks to "STE-review", "disambiguate", or "simplify" a text.

Not for creative writing, marketing copy, or prose where voice and nuance are the point.

## Two Modes

Pick a mode before rewriting.

**Strict** — procedures, error messages, tool descriptions, inter-agent instructions, inbox tasks, acceptance criteria, guardrails, safety text. Anywhere a wrong reading has a cost. Apply every structural rule and the one-word-one-meaning discipline.

**STE-flavored** — READMEs, docs overviews, plan narratives, explanatory prose. Apply the structural rules in full; treat lexical rules as advisory (keep sentence caps, active voice, simple tenses, no phrasal verbs, no semicolons, no nominalization, no marketing adjectives; relax the one-word-one-meaning lockdown).

Bitcraft default: **STE-flavored** for docs and governance, **Strict** for anything an agent executes against (inbox tasks, acceptance criteria, instructions files, prompts, skills, agent constraints).

## Core Rewrite Rules

### Structural rules — always apply

| Rule                         | Do                                                  | Don't                                                          |
| ---------------------------- | --------------------------------------------------- | -------------------------------------------------------------- |
| Active voice                 | "The agent deletes the file."                       | "The file is deleted." — unless the actor is genuinely unknown |
| No phrasal verbs             | "Remove the panel." / "Start the job."              | "Take off the panel." / "Spin up the job."                     |
| One instruction per sentence | "Open the file. Read line 3."                       | "Open the file and read line 3, then check it."                |
| Sentence length              | ≤20 words for instructions, ≤25 for descriptions    | Long compound sentences                                        |
| No semicolons                | Split into separate sentences                       | Any semicolon at all                                           |
| Noun clusters                | ≤3 words stacked                                    | 4+ word noun stacks                                            |
| No ellipsis                  | Keep subject, verb, and article explicit            | Dropping words to save space                                   |
| Keep modality                | "The request **may have** failed." stays "may have" | Promoting a hedge to a fact                                    |
| Paragraph limits             | One topic per paragraph, ≤6 sentences               | Multi-topic paragraphs                                         |
| Lists for sequences          | Numbered/bulleted list for 3+ steps or conditions   | Burying a sequence in prose                                    |

### Lexical rules — direction of travel

- One word, one meaning: pick one verb for one action and reuse it ("check" vs "verify" vs "confirm" — choose one per document).
- Prefer the verb form over the noun form ("analyze the log", not "perform an analysis of the log").
- Keep necessary technical nouns/verbs; define them once if not common English.
- The official ASD ~900-word dictionary is not reproduced here; apply the principle (plainest common word, used consistently) rather than claiming dictionary compliance.

### Simple tenses

Use infinitive, imperative, simple present/past/future. Avoid present perfect and compound forms — except where the compound form carries information the simple form cannot (current relevance, "may have failed" as a hedge). Keep those and flag the departure.

## Scan Checklist

Six mechanical habits to scan for before rewriting:

1. **Synonym rotation** — the same thing gets several names in one document. Pick one name, use it every time.
2. **Hedge stacking** — qualifiers pile up until the sentence asserts nothing. State the claim or delete it.
3. **Nominalization** — an action frozen into a noun. Use the verb.
4. **Marketing adjectives** — seamless, robust, powerful, effortless. Delete or replace with the measurement.
5. **Run-on sentences** — several ideas joined by semicolons or em dashes. One idea per sentence.
6. **Soft phrasal verbs** — spin up, reach out, dive into. Use the single plain verb.

## Process

1. Pick the mode (Strict or STE-flavored).
2. Read the input once for meaning before rewriting.
3. Walk sentence by sentence. Flag every structural violation. In STE-flavored mode, flag lexical rules but do not enforce them.
4. Rewrite each flagged sentence while preserving every fact, condition, and scope qualifier. If a rewrite would drop required precision, keep the longer phrasing and flag it instead.
   - **Check modality before committing.** Hedges carry the author's confidence. Upgrading a hedge to a fact changes the claim.
   - Never add a fact the source did not state.
5. Output the rewritten text.
6. If the input already complies, say so — do not force changes onto compliant text.

## Output Format

**Default: the rewritten text, and nothing else.** No preamble, no mode announcement, no violation count, no closing offer.

One permitted addition: if a longer phrasing was kept on purpose, add a single line prefixed `Kept as-is:` naming the phrase and the precision that would have been lost.

**On request: the rule table.** When the user asks "show the diff" or "which rules did it break", output a before/after table naming each rule violated, followed by a one-line note on anything deliberately not simplified.

## Boundaries

**Will:** rewrite ambiguous or dense English into short, single-meaning, active-voice sentences; preserve every fact, condition, qualifier, and hedge strength; suggest one-line glossary entries for domain terms that must stay.

**Will not:** reproduce the official ASD dictionary; simplify creative or persuasive copy; silently drop a safety condition or scope qualifier; convert "may have failed" into "failed"; shorten past the point of clarity — removing ambiguity is the goal, not cutting words.

## Bitcraft Usage Notes

- Apply during WS2/WS3/WS5 of the agentic stack modernization when rewriting prompts, skills, agents, and governance docs.
- Secretary applies STE-flavored review to `docs/` and reports before shipping.
- AI Ops applies Strict review to inbox tasks, acceptance criteria, and instruction files.
- Pair with `bitcraft-agentic-stack` for stack structure decisions; this skill owns prose clarity only.
