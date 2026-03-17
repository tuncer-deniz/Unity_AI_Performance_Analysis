using System;
using System.Reflection;

namespace FrameAnalyzer.Editor.Claude
{
    /// <summary>
    /// Optional integration with the Claude Code CLI for Unity package.
    /// Uses reflection to avoid a hard assembly dependency — the CLI package
    /// may or may not be installed in the project.
    /// </summary>
    public static class ClaudeCodeBridge
    {
        private static MethodInfo s_attachMethod;
        private static bool s_checked;

        /// <summary>True when the com.tonythedev.unity-claude-code-cli package is installed.</summary>
        public static bool IsAvailable
        {
            get
            {
                if (!s_checked) Detect();
                return s_attachMethod != null;
            }
        }

        private static void Detect()
        {
            s_checked = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("ClaudeCode.Editor.ClaudeCodeEditorWindow");
                if (type != null)
                {
                    s_attachMethod = type.GetMethod("AttachFileAndFocus",
                        BindingFlags.Public | BindingFlags.Static);
                    break;
                }
            }
        }

        /// <summary>
        /// Opens the Claude Code CLI window and attaches a file as conversation context.
        /// </summary>
        public static void SendFile(string path, string displayName, string typeLabel)
        {
            if (!IsAvailable) return;
            try
            {
                s_attachMethod.Invoke(null, new object[] { path, displayName, typeLabel });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[FrameAnalyzer] Failed to send to Claude Code CLI: {e.Message}");
            }
        }
    }
}
