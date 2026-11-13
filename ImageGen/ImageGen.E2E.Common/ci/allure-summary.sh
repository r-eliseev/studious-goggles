#!/usr/bin/env bash
set -euo pipefail

source_root="${1:?Usage: allure-summary.sh <download-root> <merged-results-dir> [summary-file]}"
merged_dir="${2:?Usage: allure-summary.sh <download-root> <merged-results-dir> [summary-file]}"
summary_file="${3:-${GITHUB_STEP_SUMMARY:-/dev/stdout}}"
mkdir -p "$merged_dir"

declare -A counts=(
  [android_passed]=0 [android_failed]=0 [android_skipped]=0
  [windows_passed]=0 [windows_failed]=0 [windows_skipped]=0
)
result_count=0

read_status() {
  local file="$1"
  if command -v jq >/dev/null 2>&1; then
    jq -r '.status // "unknown"' "$file"
  else
    python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8")).get("status", "unknown"))' "$file"
  fi
}

for platform in android windows; do
  platform_root="$source_root/$platform"
  [[ -d "$platform_root" ]] || continue

  while IFS= read -r -d '' result_file; do
    status="$(read_status "$result_file")"
    case "$status" in
      passed) category=passed ;;
      failed|broken) category=failed ;;
      *) category=skipped ;;
    esac
    key="${platform}_${category}"
    counts[$key]=$((counts[$key] + 1))
    result_count=$((result_count + 1))
  done < <(find "$platform_root" -type f -name '*-result.json' -print0)

  while IFS= read -r -d '' result_directory; do
    while IFS= read -r -d '' file; do
      name="${file##*/}"
      destination="$merged_dir/$name"
      if [[ -e "$destination" ]]; then
        printf 'Allure result filename collision: %s\n' "$name" >&2
        exit 1
      fi
      cp "$file" "$destination"
    done < <(find "$result_directory" -type f -print0)
  done < <(find "$platform_root" -type d -name allure-results -print0)
done

if (( result_count == 0 )); then
  printf '### Allure UI test summary\n\nNo Allure results.\n' >> "$summary_file"
  exit 0
fi

{
  printf '### Allure UI test summary\n\n'
  printf '| Platform | Passed | Failed | Skipped |\n| --- | ---: | ---: | ---: |\n'
  printf '| Android | %s | %s | %s |\n' "${counts[android_passed]}" "${counts[android_failed]}" "${counts[android_skipped]}"
  printf '| Windows | %s | %s | %s |\n' "${counts[windows_passed]}" "${counts[windows_failed]}" "${counts[windows_skipped]}"
  if [[ -n "${GITHUB_RUN_ID:-}" ]]; then
    run_url="${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:-}/actions/runs/$GITHUB_RUN_ID"
    printf '\nReport: [allure-report artifact](%s)\n' "$run_url"
  fi
} >> "$summary_file"
