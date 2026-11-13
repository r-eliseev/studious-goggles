#!/usr/bin/env bash
# Shared task-script functions. Source with: . scripts/tasks-lib.sh

T_REMOTE="${TASKS_REMOTE:-origin}"
# Tasks normally live on the current branch (which must exist on origin); TASKS_BRANCH overrides it.
_cur_branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null)
T_BRANCH="${TASKS_BRANCH:-$([ -n "$_cur_branch" ] && [ "$_cur_branch" != HEAD ] && echo "$_cur_branch" || echo main)}"

# task_field <file> <field>: value of a field in the task file header
task_field() {
  awk -v k="$2" '/^---$/{n++; next} n==1 && index($0,k":")==1 {sub("^"k":[ ]*",""); print; exit}' "$1"
}

# task_set <file> <field> <value>: writes a field in the header (adds it if missing)
task_set() {
  awk -v k="$2" -v v="$3" '
    /^---$/ { n++; if (n==2 && !done) { print k ":" (v=="" ? "" : " " v); done=1 } print; next }
    n==1 && index($0,k":")==1 { print k ":" (v=="" ? "" : " " v); done=1; next }
    { print }' "$1" > "$1.tmp" && mv "$1.tmp" "$1"
}

# task_norm_id <number>: zero-padded task number (1 -> 001)
task_norm_id() { printf '%03d' "$((10#$1))"; }

# task_done_ids <root>: task numbers in the completed-task log tasks/DONE.md
task_done_ids() {
  [ -f "$1/tasks/DONE.md" ] && sed -nE 's/^- ([0-9]+) \|.*/\1/p' "$1/tasks/DONE.md" || true
}

# task_done_at <ref>: "NNN<TAB>title" lines for tasks with done status at ref
# If a task exists both as a task file and in tasks/DONE.md, use the task file.
task_done_at() {
  local ref="$1"
  {
    git ls-tree -r --name-only "$ref" tasks 2>/dev/null | grep -E '/[0-9][0-9]*-[^/]*\.md$' | while IFS= read -r path; do
      status=$(git show "$ref:$path" 2>/dev/null | awk '/^---$/{n++; next} n==1 && index($0,"status:")==1 {sub("^status:[ ]*",""); print; exit}')
      [ "$status" = "done" ] || continue
      id=$(basename "$path" | sed -E 's/^([0-9]+)-.*/\1/')
      title=$(git show "$ref:$path" 2>/dev/null | grep -m1 '^# ' | sed 's/^# //')
      printf '0\t%s\t%s\n' "$id" "$title"
    done
    if git cat-file -e "$ref:tasks/DONE.md" 2>/dev/null; then
      git show "$ref:tasks/DONE.md" 2>/dev/null | awk -F' \\| ' '$3 == "done" { id=$1; sub(/^- /, "", id); printf "1\t%s\t%s\n", id, $5 }'
    fi
  } | sort -t$'\t' -k2,2n -k1,1 | awk -F'\t' '!seen[$2]++ { printf "%s\t%s\n", $2, $3 }'
}

# task_next_id <root>: next available task number (tasks/DONE.md numbers are never reused)
task_next_id() {
  local last
  last=$( { find "$1/tasks" -type f -name '[0-9][0-9]*-*.md' | sed -E 's|.*/([0-9]+)-[^/]*$|\1|'; task_done_ids "$1"; } | sort -n | tail -1)
  printf '%03d' $(( 10#${last:-0} + 1 ))
}

# task_file <root> <number>: path to a task file
task_file() { find "$1/tasks" -type f -name "$2-*.md" 2>/dev/null | head -1; }

# task_render <template> <number> <name>: render a task file to stdout
task_render() {
  sed -e "s/{{ID}}/$2/" -e "s/{{DATE}}/$(date +%F)/" -e "s/{{TITLE}}/$3/" "$1"
}

_task_drop() { git -C "$1" worktree remove --force "$2/wt" >/dev/null 2>&1; rm -rf "$2/wt"; }

# task_on_main <function> <commit message>
# Applies the function to a temporary copy of $T_REMOTE/$T_BRANCH, then commits and pushes.
# If another push wins the race, retries from the latest version (up to 10 times).
# The function receives the copy path and returns: 0 to commit, 100 if nothing changed,
# any other value to abort with that code. The working copy is untouched.
task_on_main() {
  local fn="$1" msg="$2" repo tmp rc i err=""
  repo=$(git rev-parse --show-toplevel) || return 1
  tmp=$(mktemp -d) || return 1
  for i in 1 2 3 4 5 6 7 8 9 10; do
    # Retry a failed fetch too: parallel processes in one clone may briefly lock the ref.
    if ! err=$(git -C "$repo" fetch -q "$T_REMOTE" "$T_BRANCH" 2>&1); then sleep 1; continue; fi
    git -C "$repo" worktree add -q --detach "$tmp/wt" "$T_REMOTE/$T_BRANCH" || { rm -rf "$tmp"; return 1; }
    "$fn" "$tmp/wt"; rc=$?
    if [ $rc -ne 0 ]; then _task_drop "$repo" "$tmp"; rm -rf "$tmp"; return $rc; fi
    git -C "$tmp/wt" add -A tasks
    if ! git -C "$tmp/wt" commit -q -m "$msg"; then
      echo "Could not create a commit (check git config user.name and user.email)" >&2
      _task_drop "$repo" "$tmp"; rm -rf "$tmp"; return 1
    fi
    if err=$(git -C "$tmp/wt" push -q "$T_REMOTE" "HEAD:$T_BRANCH" 2>&1); then
      _task_drop "$repo" "$tmp"; rm -rf "$tmp"; return 0
    fi
    _task_drop "$repo" "$tmp"
  done
  echo "Could not update $T_REMOTE/$T_BRANCH after 10 attempts. Last error: $err" >&2
  rm -rf "$tmp"; return 1
}
