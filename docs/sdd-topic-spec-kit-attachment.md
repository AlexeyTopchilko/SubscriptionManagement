# Spec Kit — Detailed

> **Timebox:** ~10–12 hours.
> **Deadline:** 2 weeks from start date.

## What and why

Spec-Driven Development (SDD) treats the **specification as the primary artifact** and generated code as regenerable output. Instead of chatting with an AI until something works, you produce a versioned spec → plan → tasks → implementation flow that the AI executes against. This reduces hallucinations, context loss, and drift across sessions.

**Spec Kit** (by GitHub) is the lightest SDD framework. It uses a linear phase structure — constitution, spec, plan, tasks, implement — and is the natural entry point for learning SDD. Best for clean greenfield features where requirements are already reasonably clear.

*Other SDD frameworks you may hear about but don't need here: OpenSpec (fluid workflow with delta specs, better for iterative brownfield work), BMAD (multi-agent SDD for complex features). These are separate topics.*

In this topic, you will install Spec Kit and complete one full cycle on a small feature you pick yourself.

---

## Step 1 — Theory (~4 hours)

Read, in order:
1. [Spec Kit README](https://github.com/github/spec-kit) — what Spec Kit is, command list
2. [`spec-driven.md`](https://github.com/github/spec-kit/blob/main/spec-driven.md) — the full methodology doc; the "why" behind the commands
3. Watch the official video overview (~15 min, linked from the README)

## Step 2 — Study the commands (~2 hours)

Read the docs for each `/speckit.*` command so you know what each produces:

| Command | Produces | Purpose |
|---|---|---|
| `/speckit.constitution` | `constitution.md` | Project principles |
| `/speckit.specify` | `spec.md` | The *what* |
| `/speckit.plan` | `plan.md` | The *how* |
| `/speckit.tasks` | `tasks.md` | 8–12 concrete tasks |
| `/speckit.implement` | code | Execute the tasks |

Skim [Community Extensions](https://github.com/github/spec-kit#community-extensions) so you know they exist.

## Step 3 — Install and initialize (~30 minutes)

```
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git@vX.Y.Z
specify version
specify check
specify init <your-project-name> --ai claude
```

Use `--ai copilot` or `--ai cursor` if that's what you use. Repo can be private.

## Step 4 — Pick a small feature

Pick something **small and self-contained** for your stack. Rules:
- Can be implemented in ~3–5 hours
- Has a clear input and output
- No complex integrations or heavy dependencies
- Runnable end-to-end in your environment

Use one of the [examples below](#example-features-by-stack) or invent your own.

## Step 5 — Run the full cycle (~3–5 hours)

In your AI assistant, commit after each step:

1. `/speckit.constitution` → review, edit if needed → commit `constitution.md`
2. `/speckit.specify` → **read the spec carefully, push back on anything vague or wrong** → commit `spec.md`
3. `/speckit.plan` → commit `plan.md`
4. `/speckit.tasks` → commit `tasks.md`
5. `/speckit.implement` → commit the generated code

Run the feature manually. If it doesn't work: **fix the spec and re-run the relevant step**. Don't patch the code directly — that defeats SDD.

---

## Definition of Done

- [ ] `specify check` passes
- [ ] All 5 artifacts committed separately: `constitution.md`, `spec.md`, `plan.md`, `tasks.md`, implementation code
- [ ] Feature works in your stack (manual smoke check)

---

## Example features by stack

Use one of these **only if you don't want to invent your own**. The learning is in the SDD loop, not the feature itself.

| Stack | Example |
|---|---|
| .NET | `/health` Minimal API endpoint returning app version, uptime, DB reachability |
| Node.js / Next.js | `/api/status` route returning build hash, env, last-deploy timestamp |
| React / frontend | `<StatusBadge />` component polling a mock status endpoint with color states |
| PHP | `/status.php` returning JSON with app version, PHP version, DB reachability |
| Ruby | Sinatra/Rails `/status` endpoint returning version, env, timestamp |

---

## Resources

- Spec Kit: https://github.com/github/spec-kit
- SDD methodology doc: https://github.com/github/spec-kit/blob/main/spec-driven.md
- Community Extensions: https://github.com/github/spec-kit#community-extensions
- Microsoft's SDD blog post: https://developer.microsoft.com/blog/spec-driven-development-spec-kit

---

## Tips

- **Keep the feature small.** The learning is in the loop, not the output. Resist scope creep.
- **Commit after every Spec Kit command.** Your git history should read as a narrative.
- **Read the AI's spec critically.** Don't just accept it. Catching misunderstandings at the spec stage is half the value of SDD.
- **If your AI assistant doesn't support slash commands,** paste the prompt from each `/speckit.*` command manually — the methodology works the same way.
- **Questions during the work? Bring them to your next 1:1.**
