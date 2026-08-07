#!/bin/bash
# Cuts a release by TAGGING. Publishing happens in GitHub Actions (.github/workflows/release.yml)
# and nowhere else — `dotnet nuget push` from a workstation is forbidden by the house standard,
# because a package nobody can reproduce from a tag is a package nobody can audit.
set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

DRY_RUN=false
VERSION=""
INCREMENT="patch"
MESSAGE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--patch) INCREMENT="patch"; shift ;;
        -n|--minor) INCREMENT="minor"; shift ;;
        -M|--major) INCREMENT="major"; shift ;;
        -m|--message) MESSAGE="$2"; shift 2 ;;
        --dry-run) DRY_RUN=true; shift ;;
        -*) echo "Unknown option: $1"; exit 1 ;;
        *) VERSION="$1"; shift ;;
    esac
done

if ! git diff --quiet || ! git diff --staged --quiet; then
    echo "Error: Uncommitted changes. Commit or stash first."
    exit 1
fi

get_latest_version() {
    git tag -l 'v*.*.*' | sort -V | tail -n1 | sed 's/^v//' || echo "0.0.0"
}

increment_version() {
    local v=$1 part=$2
    IFS='.' read -r major minor patch <<< "$v"
    case $part in
        major) echo "$((major + 1)).0.0" ;;
        minor) echo "$major.$((minor + 1)).0" ;;
        patch) echo "$major.$minor.$((patch + 1))" ;;
    esac
}

if [ -z "$VERSION" ]; then
    LATEST=$(get_latest_version)
    # A MAJOR bump means an ordinal moved in PropertyValueKind, which must never happen: those
    # ordinals are the wire format, and moving one silently re-types every value already stored by
    # every consumer. Adding a kind is a MINOR bump and is additive on both sides.
    VERSION=$(increment_version "${LATEST:-0.0.0}" "$INCREMENT")
fi

TAG="v$VERSION"

if git tag -l "$TAG" | grep -q "$TAG"; then
    echo "Error: Tag $TAG already exists"
    exit 1
fi

echo "Creating release: $TAG"

if [ "$DRY_RUN" = true ]; then
    echo "[DRY RUN] Would create and push tag: $TAG"
    exit 0
fi

if [ -n "$MESSAGE" ]; then
    git tag -a "$TAG" -m "$MESSAGE"
else
    git tag "$TAG"
fi
git push origin "$TAG"

echo "Release $TAG created. GitHub Actions publishes to nuget.modelingevolution.com first, then nuget.org."
