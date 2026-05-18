# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

The canonical, tool-agnostic guidance lives in [AGENTS.md](./AGENTS.md) and is imported below. Edit `AGENTS.md` for any changes that apply to all AI coding agents; only add Claude-specific notes in the **Claude-specific notes** section of this file.

@AGENTS.md

## Claude-specific notes

- When the user invokes `/opsx:*` slash commands, follow the routing in the user's global `CLAUDE.md`: judgment-heavy commands (`explore`, `new`, `ff`, `propose`, `continue`, `onboard`, `bulk-archive`) run on the main agent; mechanical commands (`apply`, `verify`, `archive`, `sync`) must delegate to their dedicated subagents — do not edit files yourself in those flows.
- For subagent dispatch, pass `model:` explicitly at the call site (`haiku` for `Explore` / doc lookup / verify-runner / archive-curator; `sonnet` for `Plan` / `apply-executor` / `sync-coordinator`). Never let exploration subagents inherit the Opus parent model.
- Auto-memory for this repo lives in `<project>/memory/` (per-project), not `~/.claude/`. Save user/feedback/project/reference memories there per the global rules.
