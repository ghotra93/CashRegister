#!/usr/bin/env bash
# classify-path.sh
# Shared path classifier for the PreToolUse/PostToolUse hooks.
#
# Sourced, not executed. Defines one pure function:
#
#   classify_path <file_path>   ->  prints exactly one of:
#                                     artifact  spec/build/config file, never gated
#                                     prod      production code, TDD gate applies
#                                     test      test code, files_in_scope applies
#                                     other     unrecognised, hooks ignore it
#
# Classification is by PATH SHAPE only. It deliberately does not read
# .specs/_stack.json: that file does not exist until /onboard or /net-onboard
# runs, and a hook must not fail closed on a missing file.
#
# Rule order matters. The Java rules (src/main, src/test) are evaluated before
# the C# rules so existing JVM behavior is byte-for-byte unchanged.

classify_path() {
  local p="$1"

  # Normalise so a repo-relative path ("src/main/java/App.java") matches the
  # same */prefixed patterns as an absolute one. Claude Code passes absolute
  # paths today, but the hooks are also driven by hand and from tests.
  case "$p" in
    /* | ./*) ;;        # already absolute or explicitly relative
    *) p="./$p" ;;      # bare or repo-relative: give it a leading ./
  esac

  # 1. Spec artifacts are never gated.
  case "$p" in
    *.specs/*) echo artifact; return 0 ;;
  esac

  # 2. Build and configuration files are never gated.
  case "$p" in
    */pom.xml|pom.xml) echo artifact; return 0 ;;
    */checkstyle.xml|*/dependency-check-suppressions.xml) echo artifact; return 0 ;;
    */global.json|global.json) echo artifact; return 0 ;;
    */Directory.Build.props|Directory.Build.props) echo artifact; return 0 ;;
    */Directory.Packages.props|Directory.Packages.props) echo artifact; return 0 ;;
    *.sln|*.slnx|*.csproj|*.props|*.targets) echo artifact; return 0 ;;
    */.editorconfig|.editorconfig) echo artifact; return 0 ;;
    */appsettings*.json|appsettings*.json) echo artifact; return 0 ;;
    */stryker-config.json|stryker-config.json) echo artifact; return 0 ;;
  esac

  # 3-4. Java / Maven layout, unchanged from the original hooks.
  case "$p" in
    */src/test/*) echo test; return 0 ;;
    */src/main/*) echo prod; return 0 ;;
  esac

  # 5. C# test code. Checked before C# production code so a test project
  #    living under src/ (e.g. src/Orders.Tests/) still classifies as test.
  case "$p" in
    *.cs)
      case "$p" in
        */tests/*|tests/*) echo test; return 0 ;;
        *.Tests/*|*.Tests.*/*|*.IntegrationTests/*) echo test; return 0 ;;
        *Tests.cs|*Test.cs|*Fixture.cs|*Fixtures.cs) echo test; return 0 ;;
      esac
      ;;
  esac

  # 6. C# production code.
  case "$p" in
    *.cs)
      case "$p" in
        */src/*|src/*) echo prod; return 0 ;;
      esac
      ;;
  esac

  # 7. Everything else is outside the gates.
  echo other
  return 0
}
