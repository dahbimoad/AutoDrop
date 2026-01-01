#!/bin/bash
# AutoDrop Release Script
# Usage: ./scripts/release.sh 1.0.0

set -e

VERSION=$1

if [ -z "$VERSION" ]; then
    echo "Usage: ./scripts/release.sh <version>"
    echo "Example: ./scripts/release.sh 1.0.0"
    exit 1
fi

# Validate version format
if ! [[ $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9]+)?$ ]]; then
    echo "Error: Invalid version format. Use semantic versioning (e.g., 1.0.0, 1.0.0-beta)"
    exit 1
fi

echo "🚀 Releasing AutoDrop v$VERSION"

# Ensure we're on main branch
BRANCH=$(git branch --show-current)
if [ "$BRANCH" != "main" ]; then
    echo "Warning: Not on main branch (currently on $BRANCH)"
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Ensure working directory is clean
if [ -n "$(git status --porcelain)" ]; then
    echo "Error: Working directory is not clean. Commit or stash changes first."
    exit 1
fi

# Pull latest changes
echo "📥 Pulling latest changes..."
git pull origin $BRANCH

# Update version in .csproj (optional - CI can override)
echo "📝 Version will be set by CI pipeline"

# Create and push tag
echo "🏷️  Creating tag v$VERSION..."
git tag -a "v$VERSION" -m "Release v$VERSION"

echo "📤 Pushing tag to GitHub..."
git push origin "v$VERSION"

echo ""
echo "✅ Done! GitHub Actions will now:"
echo "   1. Build for win-x64, win-x86, win-arm64"
echo "   2. Create ZIP archives"
echo "   3. Publish GitHub Release"
echo ""
echo "🔗 Watch progress at: https://github.com/YOUR_USERNAME/AutoDrop/actions"
echo ""
