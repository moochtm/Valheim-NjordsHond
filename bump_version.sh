#!/bin/bash

VERSION_FILE="VERSION.txt"

if [ ! -f "$VERSION_FILE" ]; then
    echo "VERSION.txt not found. Please create this file."
    exit 1
fi

PART="${1:-patch}"

# Read current version
VERSION=$(cat "$VERSION_FILE" | tr -d '\n')
IFS='.' read -r MAJOR MINOR PATCH <<< "$VERSION"

# Bump the appropriate part
case "$PART" in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
    *)
        echo "Invalid version part: $PART"
        exit 1
        ;;
esac

# Write the new version
NEW_VERSION="$MAJOR.$MINOR.$PATCH"
echo "$NEW_VERSION" > "$VERSION_FILE"

echo "Map Pin Manager version bumped to $NEW_VERSION"