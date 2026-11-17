---
id: 289
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Continue the dialog: walk one real flow through the cheap-failure checklist

## Description
The primary source (source 03) stops at a pending request (L2099): the owner picks one real flow (for example API Gateway -> Lambda -> Step Functions, best one where an error costs most) and describes what it does, which data it reads and writes and which external calls it makes (mail, payments, third-party APIs). Then each item of the checklist at L2057-2075 is walked: what exists, what is missing, what to add first (state protection, business-invariant alarms and synthetic runs, canary with rollback, restore checks, idempotency and replay, deferred external effects, agent isolation). Also pending in the dialog: first task class and first service for autonomy (L1461); the owner's reservations on the old-vs-new factory comparison (`docs/factory/owner-notes.md`, D1).

Depends on: the owner's answer (flow description).
Environment: any; a conversation with the owner.

## Acceptance criteria
- The owner's description of the flow is recorded in `docs/factory/` (without secrets or private data).
- A table walks every checklist item for that flow: exists / missing / first step, with the evidence the owner gave.
- The pre-mortem (D4 in owner-notes) and the assumptions list (D5) are run for the flow and recorded.
- The first task class and first service for raising autonomy are named, with the metric that would justify the next step (N1 in owner-notes).

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Waiting for the owner. Blocker: the flow description.
