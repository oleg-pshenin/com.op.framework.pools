#if ODIN_INSPECTOR
using System.Collections.Generic;
using System.IO;
using OP.Framework.Pools.SharedPool;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace OP.Framework.Pools.Editor
{
    public sealed class SharedPoolDiagnosticsWindow : OdinEditorWindow
    {
        private const double RefreshInterval = 0.25;
        private const float StatusLabelWidth = 72f;
        private const float BaseIndent = 4f;
        private const float IndentStep = 4f;
        private const float StatGap = 8f;
        private const float CreatedLabelWidth = 42f;
        private const float ActiveLabelWidth = 34f;
        private const float PooledLabelWidth = 40f;
        private const float LostLabelWidth = 24f;

        private readonly HashSet<int> _expandedEntries = new();
        private readonly HashSet<int> _expandedScopes = new();

        private Vector2 _scrollPosition;
        private PoolDebugSnapshot _snapshot;
        private bool _autoRefresh = true;
        private double _lastRefreshTime;

        private static Color LostColor => EditorGUIUtility.isProSkin ? new Color(1f, 0.45f, 0.35f) : new Color(0.75f, 0.12f, 0.08f);
        private static GUIStyle _rightMiniLabel;
        private static GUIStyle RightMiniLabel => _rightMiniLabel ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

        [MenuItem("Framework/Shared Pool Diagnostics")]
        private static void Open()
        {
            var window = GetWindow<SharedPoolDiagnosticsWindow>();
            window.titleContent = new GUIContent("Shared Pool");
            window.Show();
        }

        protected override void Initialize()
        {
            Refresh();
        }

        private void OnInspectorUpdate()
        {
            if (!_autoRefresh)
                return;

            if (EditorApplication.timeSinceStartup - _lastRefreshTime < RefreshInterval)
                return;

            Refresh();
            Repaint();
        }

        [OnInspectorGUI]
        private void DrawWindow()
        {
            DrawToolbar();

            if (!EditorApplication.isPlaying)
            {
                SirenixEditorGUI.InfoMessageBox("Enter Play Mode to inspect SharedPool.");
                return;
            }

            if (_snapshot == null)
            {
                SirenixEditorGUI.InfoMessageBox("SharedPool diagnostics are not available. Make sure SharedPool exists in the active scene.");
                return;
            }

            DrawStatusPanel();
            EditorGUILayout.Space(6);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawEntries();
            EditorGUILayout.Space(8);
            DrawScopes();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _autoRefresh = SirenixEditorGUI.ToolbarToggle(_autoRefresh, "Auto Refresh");
                GUILayout.FlexibleSpace();

                if (SirenixEditorGUI.ToolbarButton("Refresh"))
                {
                    Refresh();
                    Repaint();
                }
            }
        }

        private void DrawStatusPanel()
        {
            var created = 0;
            var active = 0;
            var pooled = 0;
            var lostActive = 0;
            var lostPooled = 0;

            foreach (var entry in _snapshot.Entries)
            {
                created += entry.CreatedCount;

                foreach (var instance in entry.Instances)
                {
                    switch (instance.State)
                    {
                        case PoolInstanceDebugState.Active:
                            active++;
                            break;
                        case PoolInstanceDebugState.Pooled:
                            pooled++;
                            break;
                        case PoolInstanceDebugState.LostActive:
                            lostActive++;
                            break;
                        case PoolInstanceDebugState.LostPooled:
                            lostPooled++;
                            break;
                    }
                }
            }

            var lost = lostActive + lostPooled;

            SirenixEditorGUI.BeginBox();
            GUILayout.Space(BaseIndent);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(BaseIndent);

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawStatusRow("Pools", _snapshot.Entries.Count.ToString());
                    DrawStatusRow("Scopes", _snapshot.Scopes.Count.ToString());
                    DrawStatusRow("Created", created.ToString());
                    DrawStatusRow("Active", active.ToString());
                    DrawStatusRow("Pooled", pooled.ToString());
                    DrawStatusRow("Lost", lost > 0 ? $"{lost} ({lostActive} active, {lostPooled} pooled)" : "0", lost > 0);
                }

                if (lost > 0)
                    GUILayout.Label(EditorGUIUtility.IconContent("console.erroricon"), GUILayout.Width(20), GUILayout.Height(20));

                GUILayout.Space(BaseIndent);
            }

            GUILayout.Space(BaseIndent);
            SirenixEditorGUI.EndBox();
        }

        private static void DrawStatusRow(string label, string value, bool highlight = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(StatusLabelWidth));

                if (highlight)
                    GUIHelper.PushContentColor(LostColor);

                GUILayout.Label(value, EditorStyles.miniLabel);

                if (highlight)
                    GUIHelper.PopContentColor();
            }
        }

        private void DrawEntries()
        {
            EditorGUILayout.LabelField("Pools", EditorStyles.boldLabel);
            SirenixEditorGUI.BeginVerticalList();

            foreach (var entry in _snapshot.Entries)
            {
                SirenixEditorGUI.BeginListItem();
                DrawEntry(entry);
                SirenixEditorGUI.EndListItem();
            }

            SirenixEditorGUI.EndVerticalList();
        }

        private void DrawEntry(PoolEntryDebugInfo entry)
        {
            var expanded = _expandedEntries.Contains(entry.Id);
            var active = 0;
            var pooled = 0;
            var lost = 0;

            foreach (var instance in entry.Instances)
            {
                switch (instance.State)
                {
                    case PoolInstanceDebugState.Active:
                        active++;
                        break;
                    case PoolInstanceDebugState.Pooled:
                        pooled++;
                        break;
                    case PoolInstanceDebugState.LostActive:
                    case PoolInstanceDebugState.LostPooled:
                        lost++;
                        break;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIndent(0);
                expanded = GUILayout.Toggle(expanded, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14));
                var typeLabel = entry.Kind == PoolEntryDebugKind.Shared ? "Shared" : "Handle";
                var callerSuffix = entry.Kind == PoolEntryDebugKind.Handle ? GetCallerTitleSuffix(entry.CallerMember, entry.CallerFile) : string.Empty;
                EditorGUILayout.LabelField($"[{typeLabel}] {entry.Name}{callerSuffix}");
                GUILayout.FlexibleSpace();
                var valueWidth = GetStatValueWidth();
                DrawStatColumn("Created", entry.CreatedCount, CreatedLabelWidth, valueWidth);
                DrawStatColumn("Active", active, ActiveLabelWidth, valueWidth);
                DrawStatColumn("Pooled", pooled, PooledLabelWidth, valueWidth);
                DrawStatColumn("Lost", lost, LostLabelWidth, valueWidth, lost > 0);
                GUILayout.Space(BaseIndent);
            }

            SetExpanded(_expandedEntries, entry.Id, expanded);

            if (!expanded)
                return;

            DrawIndentedLabel($"{entry.PrefabName} [{entry.ComponentTypeName}]", 1);
            DrawIndentedLabel($"Peak Active: {entry.PeakActive}", 1);
            if (entry.Kind == PoolEntryDebugKind.Handle)
                DrawCaller(entry.CallerMember, entry.CallerFile, entry.CallerLine, 1);
            EditorGUILayout.Space(3);

            foreach (var instance in entry.Instances)
                DrawInstance(instance, 1);
        }

        private void DrawScopes()
        {
            EditorGUILayout.LabelField("Scopes", EditorStyles.boldLabel);
            SirenixEditorGUI.BeginVerticalList();

            foreach (var scope in _snapshot.Scopes)
            {
                SirenixEditorGUI.BeginListItem();
                DrawScope(scope);
                SirenixEditorGUI.EndListItem();
            }

            SirenixEditorGUI.EndVerticalList();
        }

        private void DrawScope(PoolScopeDebugInfo scope)
        {
            var expanded = _expandedScopes.Contains(scope.Id);
            var active = 0;
            var lost = 0;

            foreach (var instance in scope.Instances)
            {
                if (instance.State == PoolInstanceDebugState.Active)
                    active++;

                if (instance.State == PoolInstanceDebugState.LostActive)
                    lost++;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIndent(0);
                expanded = GUILayout.Toggle(expanded, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14));
                EditorGUILayout.LabelField($"{scope.Name}{GetCallerTitleSuffix(scope.CallerMember, scope.CallerFile)}");
                GUILayout.FlexibleSpace();
                var valueWidth = GetStatValueWidth();
                DrawStatColumn("Active", active, ActiveLabelWidth, valueWidth);
                DrawStatColumn("Lost", lost, LostLabelWidth, valueWidth, lost > 0);
                GUILayout.Space(BaseIndent);
            }

            SetExpanded(_expandedScopes, scope.Id, expanded);

            if (!expanded)
                return;

            DrawCaller(scope.CallerMember, scope.CallerFile, scope.CallerLine, 1);
            EditorGUILayout.Space(3);

            foreach (var instance in scope.Instances)
                DrawInstance(instance, 1);
        }

        private static void DrawCaller(string callerMember, string callerFile, int callerLine, int depth)
        {
            if (string.IsNullOrEmpty(callerMember) && string.IsNullOrEmpty(callerFile))
                return;
            var file = string.IsNullOrEmpty(callerFile) ? null : Path.GetFileName(callerFile);
            var caller = GetCallerLabel(callerMember, callerFile);
            var location = file == null ? caller : $"{caller} - {file}:{callerLine}";
            DrawIndentedLabel(location, depth);
        }

        private static string GetCallerTitleSuffix(string callerMember, string callerFile)
        {
            var caller = GetCallerLabel(callerMember, callerFile);
            return string.IsNullOrEmpty(caller) ? string.Empty : $" — {caller}";
        }

        private static string GetCallerLabel(string callerMember, string callerFile)
        {
            var className = string.IsNullOrEmpty(callerFile) ? null : Path.GetFileNameWithoutExtension(callerFile);
            return string.IsNullOrEmpty(className) ? callerMember : string.IsNullOrEmpty(callerMember) ? className : $"{className}.{callerMember}";
        }

        private static void DrawInstance(PoolInstanceDebugInfo instance, int depth)
        {
            var lost = instance.State == PoolInstanceDebugState.LostActive || instance.State == PoolInstanceDebugState.LostPooled;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIndent(depth);

                if (lost)
                    GUIHelper.PushContentColor(LostColor);

                GUILayout.Label(instance.State.ToString(), GUILayout.Width(85));

                if (lost)
                    GUIHelper.PopContentColor();

                EditorGUILayout.LabelField($"{instance.Name} ({instance.InstanceId})");
                GUILayout.FlexibleSpace();

                if (!lost && instance.Instance != null && GUILayout.Button("Select", GUILayout.Width(55)))
                {
                    Selection.activeObject = instance.Instance;
                    EditorGUIUtility.PingObject(instance.Instance);
                }

                GUILayout.Space(BaseIndent);
            }
        }

        private static void DrawIndentedLabel(string text, int depth)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIndent(depth);
                EditorGUILayout.LabelField(text);
                GUILayout.Space(BaseIndent);
            }
        }

        private static void DrawIndent(int depth)
        {
            GUILayout.Space(BaseIndent + IndentStep * depth);
        }

        private void DrawStatColumn(string label, int value, float labelWidth, float valueWidth, bool highlight = false)
        {
            if (highlight)
                GUIHelper.PushContentColor(LostColor);
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(labelWidth));
            GUILayout.Label(value.ToString(), RightMiniLabel, GUILayout.Width(valueWidth));
            if (highlight)
                GUIHelper.PopContentColor();
            GUILayout.Space(StatGap);
        }

        private float GetStatValueWidth()
        {
            var maxValue = 0;
            foreach (var entry in _snapshot.Entries)
            {
                maxValue = Mathf.Max(maxValue, entry.CreatedCount);
                var active = 0;
                var pooled = 0;
                var lost = 0;
                foreach (var instance in entry.Instances)
                {
                    if (instance.State == PoolInstanceDebugState.Active)
                        active++;
                    else if (instance.State == PoolInstanceDebugState.Pooled)
                        pooled++;
                    else
                        lost++;
                }

                maxValue = Mathf.Max(maxValue, active, pooled, lost);
            }

            foreach (var scope in _snapshot.Scopes)
                maxValue = Mathf.Max(maxValue, scope.Instances.Count);
            var digits = Mathf.Max(3, maxValue.ToString().Length);
            return RightMiniLabel.CalcSize(new GUIContent(new string('0', digits))).x + 4f;
        }

        private void Refresh()
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;

            if (!EditorApplication.isPlaying)
            {
                _snapshot = null;
                return;
            }

            _snapshot = SharedPool.SharedPool.TryGetDebugSnapshot(out var snapshot) ? snapshot : null;
        }

        private static void SetExpanded(HashSet<int> set, int id, bool expanded)
        {
            if (expanded)
                set.Add(id);
            else
                set.Remove(id);
        }
    }
}
#else
using UnityEditor;
using UnityEngine;

namespace OP.Framework.Pools.Editor
{
    public sealed class SharedPoolDebuggerWindow : EditorWindow
    {
        [MenuItem("Framework/Shared Pool Debugger")]
        private static void Open()
        {
            var window = GetWindow<SharedPoolDebuggerWindow>();
            window.titleContent = new GUIContent("Shared Pool");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Shared Pool Debugger requires Odin Inspector.", MessageType.Warning);
        }
    }
}
#endif