#!/usr/bin/env bash
# Task-file checks. Exit code 1 if any check fails.
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
fail=0
allowed=" todo planned approved in-progress review done blocked cancelled "
ids=" "
filled() { awk -v h="$2" '$0=="## " h {p=1; next} /^## /{p=0} p && NF {f=1} END{exit !f}' "$1"; }
legacy_plan=$(printf '\u041f\u043b\u0430\u043d')
legacy_report=$(printf '\u041e\u0442\u0447\u0451\u0442')
legacy_approval=$(printf '\u043f\u043b\u0430\u043d \u043e\u0434\u043e\u0431\u0440\u0435\u043d')
legacy_description=$(printf '\u041e\u043f\u0438\u0441\u0430\u043d\u0438\u0435')
legacy_acceptance=$(printf '\u041a\u0440\u0438\u0442\u0435\u0440\u0438\u0438 \u0433\u043e\u0442\u043e\u0432\u043d\u043e\u0441\u0442\u0438')
legacy_analysis=$(printf '\u0410\u043d\u0430\u043b\u0438\u0437')

while IFS= read -r f; do
  id=$(task_field "$f" id); st=$(task_field "$f" status)
  as=$(task_field "$f" assignee); cl=$(task_field "$f" claimed); base=$(basename "$f")
  [ -n "$id" ] || { echo "ERROR: $f: missing id field"; fail=1; }
  [ -n "$st" ] || { echo "ERROR: $f: missing status field"; fail=1; }
  if [ -n "$id" ]; then
    case "$base" in "$id"-*) ;; *) echo "ERROR: $f: id '$id' does not match the filename"; fail=1 ;; esac
    case "$ids" in *" $id "*) echo "ERROR: $f: duplicate id $id"; fail=1 ;; esac
    ids="$ids$id "
  fi
  if [ -n "$st" ]; then
    case "$allowed" in *" $st "*) ;; *) echo "ERROR: $f: unknown status '$st'"; fail=1 ;; esac
    case "$st" in planned|approved|in-progress|review|done)
      { filled "$f" "$legacy_plan" || filled "$f" "Plan"; } || { echo "ERROR: $f: status '$st', but the Plan section is empty"; fail=1; } ;;
    esac
    if [ "$st" = "done" ]; then
      { filled "$f" "$legacy_report" || filled "$f" "Report"; } || { echo "ERROR: $f: status 'done', but the Report section is empty"; fail=1; }
    fi
    if [ "$st" = "in-progress" ] && [ -z "$as" ]; then
      echo "ERROR: $f: status 'in-progress', but assignee is missing"; fail=1
    fi
    # Finished code is forbidden in unfinished tasks: plans contain concepts only; proof-of-concept code belongs in poc/ (docs/tasks.md)
    case "$st" in done|cancelled) ;; *)
      grep -qE '^[[:space:]]*```(csharp|cs|xml|xaml)[[:space:]]*$' "$f" \
        && { echo "ERROR: $f: C#/XAML code block in task; plans contain concepts only, proof-of-concept code belongs in poc/"; fail=1; } ;;
    esac
    case "$st" in done|cancelled)
      [ -z "$as" ] || { echo "ERROR: $f: status '$st', but task is still claimed ($as): scripts/release-task.sh ${id:-N}"; fail=1; } ;;
    esac
  fi
  if [ -n "$as" ] && [ -z "$cl" ]; then
    echo "ERROR: $f: assignee is set but claimed is missing"; fail=1
  fi
done < <(find tasks -type f -name '[0-9][0-9]*-*.md' | sort)

# Planner and executor rules (ADR 0032, docs/tasks.md "Roles"): checked against the base version of each task file.
#   - the executor does not change Description, Acceptance criteria, Analysis or Plan: approved/in-progress -> in-progress/review/blocked keeps them;
#   - review: the Report is filled and the claim is released (scripts/submit-task.sh);
#   - done (newly): the Review section is filled by the planner (scripts/review-task.sh);
#   - approved (newly): a new approval record ("Plan approved" / "Plan amendment approved") is added to the file.
section_text() {  # <file> <heading> [<heading>]: the body of the first matching "## " section
  awk -v a="$2" -v b="${3:-$2}" '($0=="## " a || $0=="## " b){p=1; next} /^## /{p=0} p' "$1"
}
approvals() { grep -ciE "plan( amendment)? approved|$legacy_approval" "$1" || true; }
base="${BASE_REF:-origin/main}"
if git rev-parse --verify -q "$base^{commit}" >/dev/null 2>&1; then
  oldf=$(mktemp)
  while IFS= read -r f; do
    st=$(task_field "$f" status); as=$(task_field "$f" assignee)
    if git cat-file -e "$base:$f" 2>/dev/null; then git show "$base:$f" > "$oldf"; old=$(task_field "$oldf" status); else
      # Archived in the base (a branch that is not rebased yet still has the file): not a change of this branch.
      git show "$base:tasks/DONE.md" 2>/dev/null | grep -q "^- $(task_field "$f" id) |" && continue
      : > "$oldf"; old=""
    fi
    case "$old:$st" in approved:in-progress|approved:review|approved:blocked|in-progress:in-progress|in-progress:review|in-progress:blocked)
      for sec in "$legacy_description|Description" "$legacy_acceptance|Acceptance criteria" "$legacy_analysis|Analysis" "$legacy_plan|Plan"; do
        if [ "$(section_text "$f" "${sec%%|*}" "${sec##*|}")" != "$(section_text "$oldf" "${sec%%|*}" "${sec##*|}")" ]; then
          echo "ERROR: $f: section '${sec##*|}' changed while executing (status $old -> $st); only the planner changes it, the executor writes a question in Comments"; fail=1
        fi
      done ;;
    esac
    if [ "$st" = "review" ]; then
      { filled "$f" "$legacy_report" || filled "$f" "Report"; } || { echo "ERROR: $f: status 'review', but the Report is empty"; fail=1; }
      [ -z "$as" ] || { echo "ERROR: $f: status 'review', but the task is still claimed ($as): scripts/submit-task.sh"; fail=1; }
    fi
    if [ "$st" = "done" ] && [ "$old" != "done" ]; then
      filled "$f" "Review" || { echo "ERROR: $f: status 'done', but the 'Review' section is empty: the planner reviews the task (scripts/review-task.sh)"; fail=1; }
    fi
    # Sending a reviewed task back (review -> approved) keeps the already approved plan: no new record needed.
    if [ "$st" = "approved" ] && [ "$old" != "approved" ] && [ "$old" != "review" ]; then
      [ "$(approvals "$f")" -gt "$(approvals "$oldf")" ] || { echo "ERROR: $f: status 'approved' without a new approval record ('Plan approved' in Comments)"; fail=1; }
    fi
  done < <(find tasks -type f -name '[0-9][0-9]*-*.md' | sort)
  rm -f "$oldf"
fi

# Task numbers must not reuse numbers in the completed-task log (tasks/DONE.md)
for d in $(task_done_ids .); do
  case "$ids" in *" $d "*) echo "ERROR: number $d is already in tasks/DONE.md (completed task); renumber the new task"; fail=1 ;; esac
done
exit $fail
