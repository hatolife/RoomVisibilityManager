#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoomVisibilityManager))]
public class RoomVisibilityManagerEditor : Editor
{
    private bool _buildResultFoldout;
    private bool _derivedStateFoldout;
    private SerializedProperty _roomRoots;
    private SerializedProperty _nonRoomRoots;
    private SerializedProperty _visibilityExcludeRoots;
    private SerializedProperty _activeToggleRoots;
    private SerializedProperty _roomHiddenMatrix;
    private SerializedProperty _boundsIgnoreRoots;
    private SerializedProperty _playerPoint;
    private SerializedProperty _checkInterval;
    private SerializedProperty _startupDelaySeconds;
    private SerializedProperty _enableDebugLogs;
    private SerializedProperty _boundsMargin;
    private SerializedProperty _boundsHysteresisMargin;
    private SerializedProperty _controlTerrains;
    private SerializedProperty _boundingBoxMargin;
    private SerializedProperty _boundingBoxParent;
    private SerializedProperty _boundingBoxNamePrefix;
    private SerializedProperty _reuseExistingBoundingBoxes;
    private SerializedProperty _includeInactiveRenderers;
    private SerializedProperty _lastBuildReport;
    private SerializedProperty _lastCollectedRendererCount;
    private SerializedProperty _lastControlledRendererCount;
    private SerializedProperty _lastExcludedRendererCount;
    private SerializedProperty _lastGeneratedBoundingBoxCount;
    private SerializedProperty _currentRoomIndex;

    private void OnEnable()
    {
        _roomRoots = serializedObject.FindProperty("roomRoots");
        _nonRoomRoots = serializedObject.FindProperty("nonRoomRoots");
        _visibilityExcludeRoots = serializedObject.FindProperty("visibilityExcludeRoots");
        _activeToggleRoots = serializedObject.FindProperty("activeToggleRoots");
        _roomHiddenMatrix = serializedObject.FindProperty("roomHiddenMatrix");
        _boundsIgnoreRoots = serializedObject.FindProperty("boundsIgnoreRoots");
        _playerPoint = serializedObject.FindProperty("playerPoint");
        _checkInterval = serializedObject.FindProperty("checkInterval");
        _startupDelaySeconds = serializedObject.FindProperty("startupDelaySeconds");
        _enableDebugLogs = serializedObject.FindProperty("enableDebugLogs");
        _boundsMargin = serializedObject.FindProperty("boundsMargin");
        _boundsHysteresisMargin = serializedObject.FindProperty("boundsHysteresisMargin");
        _controlTerrains = serializedObject.FindProperty("controlTerrains");
        _boundingBoxMargin = serializedObject.FindProperty("boundingBoxMargin");
        _boundingBoxParent = serializedObject.FindProperty("boundingBoxParent");
        _boundingBoxNamePrefix = serializedObject.FindProperty("boundingBoxNamePrefix");
        _reuseExistingBoundingBoxes = serializedObject.FindProperty("reuseExistingBoundingBoxes");
        _includeInactiveRenderers = serializedObject.FindProperty("includeInactiveRenderers");
        _lastBuildReport = serializedObject.FindProperty("lastBuildReport");
        _lastCollectedRendererCount = serializedObject.FindProperty("lastCollectedRendererCount");
        _lastControlledRendererCount = serializedObject.FindProperty("lastControlledRendererCount");
        _lastExcludedRendererCount = serializedObject.FindProperty("lastExcludedRendererCount");
        _lastGeneratedBoundingBoxCount = serializedObject.FindProperty("lastGeneratedBoundingBoxCount");
        _currentRoomIndex = serializedObject.FindProperty("currentRoomIndex");
    }

    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
        {
            return;
        }

        RoomVisibilityManager manager = (RoomVisibilityManager)target;
        serializedObject.Update();

        DrawOverview();

        EditorGUILayout.Space(10f);
        DrawUserSettings();
        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(manager);
        }

        EditorGUILayout.Space(10f);
        DrawButtons(manager);

        EditorGUILayout.Space(10f);
        DrawBuildResult();

        EditorGUILayout.Space(10f);
        DrawDerivedState(manager);
    }

    private void DrawOverview()
    {
        EditorGUILayout.HelpBox(
            "プレーヤーの現在位置ごとに、各部屋を表示するか非表示にするかを表形式で設定できます。\n" +
            "セットアップ実行で各部屋の判定用バウンディングボックス生成、表示制御対象の収集、設定チェックをまとめて行います。Renderer / Canvas / Light / Terrain を対象にできます。部屋同士の重なりは未対応で、検出時は警告します。",
            MessageType.Info
        );
    }

    private void DrawUserSettings()
    {
        EditorGUILayout.LabelField(new GUIContent("1. User Settings", "このセクションでは、部屋の構成、表示切替ルール、判定方法、バウンディングボックス生成条件など、ユーザーが直接調整する基本設定を行います。"), EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_roomRoots, new GUIContent("部屋ルートオブジェクトリスト", "部屋として扱いたい GameObject のルートを並べます。各要素の子孫全体を 1 部屋として扱います。設定を変えたあとは、セットアップ実行で判定用バウンディングボックスと表示制御対象を更新してください。"));
        EditorGUILayout.PropertyField(_nonRoomRoots, new GUIContent("部屋以外のオブジェクトのルート(部屋ルートオブジェクトが子要素に含まれていてもよい)", "部屋そのものではないが、この仕組みで表示・非表示を切り替えたいオブジェクトの親を入れます。外景、遠景、廊下、共用装飾などを想定しています。ここに入れた対象の ON/OFF は部屋表示制御表の『室外』列で決まります。"));
        EditorGUILayout.PropertyField(_visibilityExcludeRoots, new GUIContent("表示切替対象外のオブジェクトリスト", "このリスト配下は roomRoots / nonRoomRoots 配下にあっても表示切替対象から外します。常に見せたい床、補助 Quad、ギミック、UI などを入れます。Renderer / Canvas / Light / Terrain に対応しますが、GameObject ごと無効化される親の子にあると一緒に消えます。"));
        EditorGUILayout.PropertyField(_activeToggleRoots, new GUIContent("GameObject 有効状態で切り替えるオブジェクトリスト", "このリスト配下は Renderer.enabled などの個別切り替えではなく、GameObject.SetActive で親ごと ON/OFF します。複数コンポーネントをまとめて切り替えたい構成向けです。除外対象の子をここに入れると、親の無効化に巻き込まれます。"));
        DrawRoomVisibilityMatrix((RoomVisibilityManager)target);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(new GUIContent("判定設定", "プレーヤーがどの部屋にいるかをどう判定するかを決める設定群です。判定点、判定頻度、開始タイミング、境界の余裕量などを調整します。"), EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_playerPoint, new GUIContent("プレーヤー判定点", "プレーヤーがどの部屋にいるかを判定するときの基準点です。通常は AvatarRoot を使います。Head にすると、頭位置で部屋判定したい構成に向きます。"));
        EditorGUILayout.PropertyField(_checkInterval, new GUIContent("判定間隔", "部屋判定を何秒ごとに行うかです。短いほど追従は速くなりますが、そのぶん判定回数は増えます。"));
        EditorGUILayout.PropertyField(_startupDelaySeconds, new GUIContent("開始待機秒数", "ワールド入場後、この秒数が経過するまで部屋判定と表示切替を始めません。入場直後の初期化や位置確定待ちで誤判定しやすい場合に使います。"));
        EditorGUILayout.PropertyField(_boundsMargin, new GUIContent("判定マージン", "判定用バウンディングボックスの境界に追加する余裕です。境界で判定が切れやすいときに少し増やします。大きくしすぎると隣室へ食い込みやすくなります。"));
        EditorGUILayout.PropertyField(_boundsHysteresisMargin, new GUIContent("判定ヒステリシス", "現在いる部屋に留まる判定へ追加する余裕です。0 より大きい値にすると、入るときより出るときの判定を少し緩くし、境界でのチラつきを抑えやすくします。0 なら無効です。"));
        EditorGUILayout.PropertyField(_controlTerrains, new GUIContent("Terrain も非表示にする", "ON のとき、nonRoomRoots 配下の Terrain も切り替え対象に含めます。Terrain は描画フラグではなく GameObject 単位で ON/OFF されます。"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(new GUIContent("バウンディングボックス設定", "部屋判定に使うバウンディングボックスの自動生成条件を調整します。部屋サイズの余白、除外オブジェクト、生成先、命名規則などを設定できます。"), EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_boundingBoxMargin, new GUIContent("バウンディングボックスマージン", "セットアップ実行で部屋バウンディングボックスを自動生成するとき、X/Y/Z 方向へ足す余白です。少し大きめに囲いたいときに使いますが、増やしすぎると部屋同士が重なりやすくなります。"));
        EditorGUILayout.PropertyField(_boundsIgnoreRoots, new GUIContent("バウンディングボックス計算から除外するオブジェクトのルートリスト", "部屋の大きさ計算に含めたくないオブジェクトを指定します。巨大なエフェクト、QvPen、遠くまで伸びる装飾などが原因で部屋サイズが異常に膨らむときに使います。"));
        EditorGUILayout.PropertyField(_boundingBoxParent, new GUIContent("生成先の親", "自動生成したバウンディングボックスを配置する親 Transform です。未設定ならこの Manager の子として生成します。Hierarchy を整理したいときだけ変更してください。"));
        EditorGUILayout.PropertyField(_boundingBoxNamePrefix, new GUIContent("自動生成時の命名規則", "自動生成されるバウンディングボックス名の先頭に付く文字列です。例: `BoundingBox_` を入れると `BoundingBox_部屋名` のような名前になります。"));
        EditorGUILayout.PropertyField(_reuseExistingBoundingBoxes, new GUIContent("既存バウンディングボックスを再利用", "互換項目です。現在の実装では、セットアップ実行時に既存バウンディングボックスを削除して作り直すため、実質的には効きません。"));
        EditorGUILayout.PropertyField(_includeInactiveRenderers, new GUIContent("inactive 子孫も探索", "非アクティブな子オブジェクトも Bounds 計算や制御対象探索に含めます。普段は見えていないが、あとで表示する可能性があるオブジェクトを収集したい場合に使います。"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(new GUIContent("デバッグ", "調査用のログ出力設定です。通常は触らず、問題調査時だけ使う想定です。"), EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enableDebugLogs, new GUIContent("詳細デバッグログ", "ON のとき、[RoomVisibilityManager] プレフィックス付きで部屋判定と表示切替の詳細ログを Unity Console に出します。通常時は OFF、原因調査時だけ ON を推奨します。"));
    }

    private void DrawButtons(RoomVisibilityManager manager)
    {
        EditorGUILayout.LabelField(new GUIContent("2. Setup", "設定内容をもとに、判定用バウンディングボックスの生成、表示制御対象の収集、検証を実行する操作セクションです。設定変更後は基本的にここを再実行します。"), EditorStyles.boldLabel);

        if (GUILayout.Button(new GUIContent("セットアップ実行", "現在の設定を使ってセットアップを実行します。部屋バウンディングボックスの作成・更新、Renderer / Canvas / Light / Terrain / ActiveToggle の収集、重なりや設定不備の検証をまとめて行います。"), GUILayout.Height(30f)))
        {
            PreserveRendererStates(manager, () => RunSetup(manager));
        }
    }

    private void DrawRoomVisibilityMatrix(RoomVisibilityManager manager)
    {
        EnsureRoomVisibilityMatrix(manager);

        GameObject[] rooms = BuildDetectedRooms(manager);
        if (rooms.Length == 0)
        {
            EditorGUILayout.HelpBox("部屋ルートオブジェクトリストを設定すると、室外/各部屋ごとの表示制御表を編集できます。", MessageType.None);
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(new GUIContent("部屋表示制御表", "行はプレーヤーの現在位置、列は表示対象です。室外と各部屋の組み合わせごとに、対象を表示するか非表示にするかを決めます。"), EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("行は現在位置、列は表示対象です。チェックを入れると、その位置にいるとき対応する対象を表示します。", MessageType.None);

        const float labelWidth = 110f;
        const float cellWidth = 56f;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("現在位置", "各行が表す『プレーヤーが今いる場所』です。室外または各部屋を示します。"), GUILayout.Width(labelWidth));
        for (int targetIndex = 0; targetIndex < rooms.Length + 1; targetIndex++)
        {
            GUILayout.Label(GetStateLabel(rooms, targetIndex), EditorStyles.miniBoldLabel, GUILayout.Width(cellWidth));
        }
        EditorGUILayout.EndHorizontal();

        for (int stateIndex = 0; stateIndex < rooms.Length + 1; stateIndex++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(GetStateLabel(rooms, stateIndex), GUILayout.Width(labelWidth));

            for (int targetIndex = 0; targetIndex < rooms.Length + 1; targetIndex++)
            {
                if (stateIndex == targetIndex)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Toggle(true, GUILayout.Width(cellWidth));
                    }
                    continue;
                }

                int matrixIndex = stateIndex * (rooms.Length + 1) + targetIndex;
                SerializedProperty cell = _roomHiddenMatrix.GetArrayElementAtIndex(matrixIndex);
                cell.boolValue = EditorGUILayout.Toggle(cell.boolValue, GUILayout.Width(cellWidth));
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawBuildResult()
    {
        EditorGUILayout.LabelField(new GUIContent("3. Last Button Result", "最後に実行したセットアップや検証の結果を表示します。エラー、警告、補足メッセージをここで確認できます。"), EditorStyles.boldLabel);

        string report = _lastBuildReport.stringValue;
        string errors;
        string warnings;
        string otherMessages;
        SplitReportMessages(report, out errors, out warnings, out otherMessages);
        bool hasErrors = !string.IsNullOrEmpty(errors);
        bool hasWarnings = !string.IsNullOrEmpty(warnings);
        bool hasIssues = hasErrors || hasWarnings;

        if (hasIssues)
        {
            _buildResultFoldout = true;
        }

        DrawStatusSummary(
            hasErrors ? "Error" : (hasWarnings ? "Warning" : "OK"),
            hasErrors ? MessageType.Error : (hasWarnings ? MessageType.Warning : MessageType.Info),
            BuildBuildResultSummary(hasErrors, hasWarnings, errors, warnings, otherMessages)
        );

        _buildResultFoldout = EditorGUILayout.Foldout(_buildResultFoldout, new GUIContent("詳細", "最後の実行結果の詳細メッセージを開閉します。エラーや警告の本文、補足ログを確認できます。"), true);
        if (!_buildResultFoldout)
        {
            return;
        }

        if (hasErrors)
        {
            EditorGUILayout.HelpBox(errors, MessageType.Error);
        }

        if (hasWarnings)
        {
            EditorGUILayout.HelpBox(warnings, MessageType.Warning);
        }

        EditorGUILayout.TextArea(string.IsNullOrEmpty(otherMessages) ? "(No non-warning messages)" : otherMessages, GUILayout.MinHeight(72f));
    }

    private void DrawDerivedState(RoomVisibilityManager manager)
    {
        EditorGUILayout.LabelField(new GUIContent("4. Final Derived State", "現在の設定から最終的に構築された内部状態の要約です。実際に Runtime が参照する部屋数、制御対象数、現在部屋などを確認できます。"), EditorStyles.boldLabel);
        bool hasIssues = HasDerivedStateIssues(manager);
        if (hasIssues)
        {
            _derivedStateFoldout = true;
        }

        DrawStatusSummary(
            hasIssues ? "Needs Attention" : "OK",
            hasIssues ? MessageType.Warning : MessageType.Info,
            BuildDerivedStateSummary(manager)
        );

        _derivedStateFoldout = EditorGUILayout.Foldout(_derivedStateFoldout, new GUIContent("詳細", "派生データの内訳を開閉します。収集数、部屋別集計、バウンディングボックス一覧などの確認に使います。"), true);
        if (!_derivedStateFoldout)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            DrawReadOnlyIntField("収集 Renderer 数", "探索対象のルート配下で見つかった Renderer 総数です。制御対象・除外対象・最終的に採用されなかったものも含む、中間集計の値です。", _lastCollectedRendererCount.intValue);
            DrawReadOnlyIntField("制御対象 Renderer 数", "実際に表示切替対象として採用された Renderer 数です。主に nonRoomRoots 側の切替対象を表します。", _lastControlledRendererCount.intValue);
            DrawReadOnlyIntField("除外対象 Renderer 数", "visibilityExcludeRoots により常時表示扱いに分類された Renderer 数です。", _lastExcludedRendererCount.intValue);
            DrawReadOnlyIntField("制御対象 Canvas 数", "表示切替対象として採用された Canvas 数です。", manager.controlledCanvases == null ? 0 : manager.controlledCanvases.Length);
            DrawReadOnlyIntField("除外対象 Canvas 数", "常時表示扱いとして維持される Canvas 数です。", manager.excludedCanvases == null ? 0 : manager.excludedCanvases.Length);
            DrawReadOnlyIntField("制御対象 Light 数", "表示切替対象として採用された Light 数です。", manager.controlledLights == null ? 0 : manager.controlledLights.Length);
            DrawReadOnlyIntField("除外対象 Light 数", "常時表示扱いとして維持される Light 数です。", manager.excludedLights == null ? 0 : manager.excludedLights.Length);
            DrawReadOnlyIntField("生成済みバウンディングボックス数", "有効な部屋バウンディングボックス数です。部屋数より少ない場合は、一部の部屋で生成に失敗している可能性があります。", _lastGeneratedBoundingBoxCount.intValue);
            DrawReadOnlyIntField("現在部屋インデックス", "Runtime が最後に判定した現在部屋のインデックスです。-1 は室外を意味します。", _currentRoomIndex.intValue);
            DrawReadOnlyTextField("現在部屋名", "Runtime が最後に判定した現在部屋の名前です。部屋外なら `(Outside)` が表示されます。", manager.GetCurrentRoomName());
        }

        DrawDerivedObjectBreakdown(manager);
        DrawBoundingBoxBreakdown(manager);

        if (manager.detectedRoomRoots != null && manager.roomRendererCounts != null && manager.roomTotalRendererCounts != null)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent("部屋ごとの Renderer 数", "各部屋について、『表示切替対象として採用された Renderer 数 / 配下で見つかった Renderer 総数』を表示します。部屋ごとの収集漏れ確認に使います。"), EditorStyles.boldLabel);

            int length = Mathf.Min(manager.detectedRoomRoots.Length, Mathf.Min(manager.roomRendererCounts.Length, manager.roomTotalRendererCounts.Length));
            for (int i = 0; i < length; i++)
            {
                string roomName = manager.detectedRoomRoots[i] == null ? "(Missing)" : manager.detectedRoomRoots[i].name;
                EditorGUILayout.LabelField(roomName, manager.roomRendererCounts[i] + " / " + manager.roomTotalRendererCounts[i]);
            }
        }
    }

    private static void DrawStatusSummary(string title, MessageType messageType, string summary)
    {
        EditorGUILayout.HelpBox(title + "\n" + summary, messageType);
    }

    private static string BuildBuildResultSummary(bool hasErrors, bool hasWarnings, string errors, string warnings, string otherMessages)
    {
        int errorCount = CountReportLines(errors);
        int warningCount = CountReportLines(warnings);
        int infoCount = CountReportLines(otherMessages);

        if (hasErrors || hasWarnings)
        {
            return "Errors: " + errorCount + " / Warnings: " + warningCount + " / Messages: " + infoCount;
        }

        return infoCount > 0 ? ("Messages: " + infoCount) : "No messages";
    }

    private static string BuildDerivedStateSummary(RoomVisibilityManager manager)
    {
        if (manager == null)
        {
            return "No manager";
        }

        return
            "Rooms: " + manager.GetConfiguredRoomCount() +
            " / BoundingBoxes: " + manager.GetValidBoundingBoxCount() +
            " / Current: " + manager.GetCurrentRoomName() +
            " / Non-Room Renderer: " + SafeLength(manager.controlledRenderers) +
            " / Canvas: " + SafeLength(manager.controlledCanvases) +
            " / Light: " + SafeLength(manager.controlledLights) +
            " / Terrain: " + SafeLength(manager.controlledTerrainObjects) +
            " / ActiveToggle: " + SafeLength(manager.controlledActiveObjects);
    }

    private static bool HasDerivedStateIssues(RoomVisibilityManager manager)
    {
        if (manager == null)
        {
            return true;
        }

        if (manager.GetConfiguredRoomCount() == 0)
        {
            return true;
        }

        if (manager.GetValidBoundingBoxCount() < manager.GetConfiguredRoomCount())
        {
            return true;
        }

        bool hasAnyNonRoomTarget =
            SafeLength(manager.controlledRenderers) > 0 ||
            SafeLength(manager.controlledCanvases) > 0 ||
            SafeLength(manager.controlledLights) > 0 ||
            SafeLength(manager.controlledTerrainObjects) > 0 ||
            SafeLength(manager.controlledActiveObjects) > 0;

        return !hasAnyNonRoomTarget;
    }

    private static void DrawDerivedObjectBreakdown(RoomVisibilityManager manager)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(new GUIContent("対象数内訳", "非部屋対象、部屋対象、除外対象など、表示切替に使う各カテゴリの件数内訳です。"), EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            DrawReadOnlyIntField("非部屋 Renderer", "nonRoomRoots 側で Renderer.enabled により切り替える対象数です。", SafeLength(manager.controlledRenderers));
            DrawReadOnlyIntField("非部屋 Canvas", "nonRoomRoots 側で Canvas.enabled により切り替える対象数です。", SafeLength(manager.controlledCanvases));
            DrawReadOnlyIntField("非部屋 Light", "nonRoomRoots 側で Light.enabled により切り替える対象数です。", SafeLength(manager.controlledLights));
            DrawReadOnlyIntField("非部屋 Terrain", "nonRoomRoots 側で GameObject 単位で切り替える Terrain 数です。", SafeLength(manager.controlledTerrainObjects));
            DrawReadOnlyIntField("非部屋 ActiveToggle", "nonRoomRoots 側で GameObject.SetActive により切り替える対象数です。", SafeLength(manager.controlledActiveObjects));
            DrawReadOnlyIntField("部屋 ActiveToggle", "roomRoots 側で GameObject.SetActive により切り替える対象数です。", SafeLength(manager.roomActiveObjects));
            DrawReadOnlyIntField("除外 Renderer", "表示切替対象外として常時表示を維持する Renderer 数です。", SafeLength(manager.excludedRenderers));
            DrawReadOnlyIntField("除外 Canvas", "表示切替対象外として常時表示を維持する Canvas 数です。", SafeLength(manager.excludedCanvases));
            DrawReadOnlyIntField("除外 Light", "表示切替対象外として常時表示を維持する Light 数です。", SafeLength(manager.excludedLights));
        }
    }

    private static void DrawBoundingBoxBreakdown(RoomVisibilityManager manager)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(new GUIContent("部屋ごとのバウンディングボックス", "各部屋に対応する判定用バウンディングボックス名とサイズを表示します。自動生成結果が意図どおりかを確認するための一覧です。"), EditorStyles.boldLabel);

        if (manager.detectedRoomRoots == null || manager.detectedRoomRoots.Length == 0)
        {
            EditorGUILayout.LabelField("(No rooms)");
            return;
        }

        for (int i = 0; i < manager.detectedRoomRoots.Length; i++)
        {
            GameObject roomRoot = manager.detectedRoomRoots[i];
            string roomName = roomRoot == null ? "(Missing)" : roomRoot.name;
            BoxCollider boundingBox = manager.roomBoundingBoxes != null && i < manager.roomBoundingBoxes.Length ? manager.roomBoundingBoxes[i] : null;
            string boundingBoxName = boundingBox == null ? "(Missing Bounding Box)" : boundingBox.name;
            string boundingBoxSize = boundingBox == null ? "-" : Vector3ToShortString(boundingBox.size);
            EditorGUILayout.LabelField(roomName, boundingBoxName + " / size " + boundingBoxSize);
        }
    }

    private static int SafeLength(Array array)
    {
        return array == null ? 0 : array.Length;
    }

    private static void DrawReadOnlyIntField(string label, string tooltip, int value)
    {
        EditorGUILayout.IntField(new GUIContent(label, tooltip), value);
    }

    private static void DrawReadOnlyTextField(string label, string tooltip, string value)
    {
        EditorGUILayout.TextField(new GUIContent(label, tooltip), value);
    }

    private static int CountReportLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length;
    }

    private static string Vector3ToShortString(Vector3 value)
    {
        return value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##");
    }

    private void GenerateBoundingBoxes(RoomVisibilityManager manager)
    {
        StringBuilder report = new StringBuilder();
        GenerateBoundingBoxes(manager, report, true);
    }

    private void GenerateBoundingBoxes(RoomVisibilityManager manager, StringBuilder report, bool writeReport)
    {
        if (report == null)
        {
            report = new StringBuilder();
        }

        if (!ValidateRequiredSetup(manager, report))
        {
            WriteReport(manager, report.ToString());
            return;
        }

        Undo.RegisterCompleteObjectUndo(manager, "Generate Bounding Boxes");

        Transform boundingBoxParent = manager.boundingBoxParent == null ? manager.transform : manager.boundingBoxParent;
        GameObject[] roomRoots = BuildDetectedRooms(manager);
        BoxCollider[] generatedBoundingBoxes = new BoxCollider[roomRoots.Length];
        int generatedCount = 0;
        manager.detectedRoomRoots = roomRoots;

        if (manager.roomBoundingBoxes != null)
        {
            for (int i = 0; i < manager.roomBoundingBoxes.Length; i++)
            {
                if (manager.roomBoundingBoxes[i] != null)
                {
                    Undo.DestroyObjectImmediate(manager.roomBoundingBoxes[i].gameObject);
                }
            }
        }

        for (int i = 0; i < roomRoots.Length; i++)
        {
            GameObject roomRoot = roomRoots[i];
            Bounds bounds;
            if (!TryCollectRoomBounds(roomRoot, manager.boundsIgnoreRoots, manager.includeInactiveRenderers, out bounds, report))
            {
                report.AppendLine("Warning: " + roomRoot.name + " のバウンディングボックス生成に必要な Bounds を取得できませんでした。");
                continue;
            }

            BoxCollider existingBoundingBox = null;
            BoxCollider boundingBox = existingBoundingBox;
            if (boundingBox == null)
            {
                GameObject boundingBoxObject = new GameObject(BuildBoundingBoxName(manager, roomRoot, i));
                Undo.RegisterCreatedObjectUndo(boundingBoxObject, "Create Bounding Box");
                boundingBoxObject.transform.SetParent(boundingBoxParent, true);
                boundingBox = boundingBoxObject.AddComponent<BoxCollider>();
                boundingBox.isTrigger = true;
                RoomVisibilityBoundingBox helper = boundingBoxObject.AddComponent<RoomVisibilityBoundingBox>();
                helper.roomRoot = roomRoot;
            }

            ConfigureBoundingBox(boundingBox, roomRoot, bounds, manager.boundingBoxMargin);
            generatedBoundingBoxes[i] = boundingBox;
            generatedCount++;
        }

        manager.roomBoundingBoxes = generatedBoundingBoxes;
        manager.lastGeneratedBoundingBoxCount = generatedCount;
        EditorUtility.SetDirty(manager);

        ShowBoundingBoxOverlapDialogIfNeeded(manager, generatedBoundingBoxes, report);
        report.AppendLine("Generated Bounding Boxes: " + generatedCount + " / " + roomRoots.Length);
        RebuildCache(manager, writeReport, report);
    }

    private void RebuildCache(RoomVisibilityManager manager, bool overwriteReport)
    {
        RebuildCache(manager, overwriteReport, new StringBuilder());
    }

    private void RebuildCache(RoomVisibilityManager manager, bool overwriteReport, StringBuilder report)
    {
        if (!ValidateRequiredSetup(manager, report))
        {
            WriteReport(manager, report.ToString());
            return;
        }

        Undo.RegisterCompleteObjectUndo(manager, "Rebuild Room Visibility Cache");

        GameObject[] roomRoots = BuildDetectedRooms(manager);
        manager.detectedRoomRoots = roomRoots;

        List<Renderer> controlled = new List<Renderer>();
        List<int> roomIndices = new List<int>();
        List<Renderer> excluded = new List<Renderer>();
        HashSet<Renderer> excludedSet = new HashSet<Renderer>();
        List<Renderer> roomRendererList = new List<Renderer>();
        List<int> roomRendererRoomIndices = new List<int>();
        List<Canvas> controlledCanvases = new List<Canvas>();
        List<Canvas> excludedCanvases = new List<Canvas>();
        HashSet<Canvas> excludedCanvasSet = new HashSet<Canvas>();
        List<Canvas> roomCanvasList = new List<Canvas>();
        List<int> roomCanvasRoomIndices = new List<int>();
        List<Light> controlledLights = new List<Light>();
        List<Light> excludedLights = new List<Light>();
        HashSet<Light> excludedLightSet = new HashSet<Light>();
        List<Light> roomLightList = new List<Light>();
        List<int> roomLightRoomIndices = new List<int>();
        List<GameObject> controlledTerrainObjects = new List<GameObject>();
        List<GameObject> controlledActiveObjects = new List<GameObject>();
        List<GameObject> roomActiveObjects = new List<GameObject>();
        List<int> roomActiveObjectRoomIndices = new List<int>();
        HashSet<GameObject> activeObjectSet = new HashSet<GameObject>();
        int[] roomRendererCounts = new int[roomRoots.Length];
        int[] roomTotalRendererCounts = new int[roomRoots.Length];
        int totalRoomRenderers = 0;
        int totalRoomCanvases = 0;
        int totalRoomLights = 0;

        CollectActiveToggleObjects(manager, roomRoots, controlledActiveObjects, roomActiveObjects, roomActiveObjectRoomIndices, activeObjectSet);

        for (int roomIndex = 0; roomIndex < roomRoots.Length; roomIndex++)
        {
            GameObject roomRoot = roomRoots[roomIndex];
            if (roomRoot == null)
            {
                continue;
            }

            Renderer[] roomRenderers = roomRoot.GetComponentsInChildren<Renderer>(manager.includeInactiveRenderers);
            for (int i = 0; i < roomRenderers.Length; i++)
            {
                Renderer renderer = roomRenderers[i];
                if (!IsEligibleForVisibilityControl(renderer))
                {
                    continue;
                }

                roomTotalRendererCounts[roomIndex]++;

                if (IsExcluded(renderer.transform, manager.visibilityExcludeRoots))
                {
                    AddUniqueRenderer(excluded, excludedSet, renderer);
                    continue;
                }

                if (IsUnderAnyRoot(renderer.transform, manager.activeToggleRoots))
                {
                    continue;
                }

                roomRendererCounts[roomIndex]++;
                totalRoomRenderers++;
                roomRendererList.Add(renderer);
                roomRendererRoomIndices.Add(roomIndex);
            }

            Canvas[] roomCanvases = roomRoot.GetComponentsInChildren<Canvas>(manager.includeInactiveRenderers);
            for (int i = 0; i < roomCanvases.Length; i++)
            {
                Canvas canvas = roomCanvases[i];
                if (!IsEligibleForVisibilityControl(canvas))
                {
                    continue;
                }

                if (IsExcluded(canvas.transform, manager.visibilityExcludeRoots))
                {
                    AddUniqueCanvas(excludedCanvases, excludedCanvasSet, canvas);
                    continue;
                }

                if (IsUnderAnyRoot(canvas.transform, manager.activeToggleRoots))
                {
                    continue;
                }

                totalRoomCanvases++;
                roomCanvasList.Add(canvas);
                roomCanvasRoomIndices.Add(roomIndex);
            }

            Light[] roomLights = roomRoot.GetComponentsInChildren<Light>(manager.includeInactiveRenderers);
            for (int i = 0; i < roomLights.Length; i++)
            {
                Light light = roomLights[i];
                if (!IsEligibleForVisibilityControl(light))
                {
                    continue;
                }

                if (IsExcluded(light.transform, manager.visibilityExcludeRoots))
                {
                    AddUniqueLight(excludedLights, excludedLightSet, light);
                    continue;
                }

                if (IsUnderAnyRoot(light.transform, manager.activeToggleRoots))
                {
                    continue;
                }

                totalRoomLights++;
                roomLightList.Add(light);
                roomLightRoomIndices.Add(roomIndex);
            }
        }

        Renderer[] renderers = CollectCandidateRenderers(manager);
        Canvas[] canvases = CollectCandidateCanvases(manager);
        Light[] lights = CollectCandidateLights(manager);
        int nonRoomRenderers = 0;
        int nonRoomCanvases = 0;
        int nonRoomLights = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!IsEligibleForVisibilityControl(renderer))
            {
                continue;
            }

            if (IsExcluded(renderer.transform, manager.visibilityExcludeRoots))
            {
                AddUniqueRenderer(excluded, excludedSet, renderer);
                continue;
            }

            if (IsUnderAnyRoomRoot(renderer.transform, roomRoots))
            {
                continue;
            }

            if (IsUnderAnyRoot(renderer.transform, manager.activeToggleRoots))
            {
                continue;
            }

            if (IsExcluded(renderer.transform, manager.nonRoomRoots))
            {
                controlled.Add(renderer);
                roomIndices.Add(-1);
                nonRoomRenderers++;
            }
            else
            {
                AddUniqueRenderer(excluded, excludedSet, renderer);
            }
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            if (!IsEligibleForVisibilityControl(canvas))
            {
                continue;
            }

            if (IsExcluded(canvas.transform, manager.visibilityExcludeRoots))
            {
                AddUniqueCanvas(excludedCanvases, excludedCanvasSet, canvas);
                continue;
            }

            if (IsUnderAnyRoomRoot(canvas.transform, roomRoots))
            {
                continue;
            }

            if (IsUnderAnyRoot(canvas.transform, manager.activeToggleRoots))
            {
                continue;
            }

            if (IsExcluded(canvas.transform, manager.nonRoomRoots))
            {
                controlledCanvases.Add(canvas);
                nonRoomCanvases++;
            }
            else
            {
                AddUniqueCanvas(excludedCanvases, excludedCanvasSet, canvas);
            }
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            if (!IsEligibleForVisibilityControl(light))
            {
                continue;
            }

            if (IsExcluded(light.transform, manager.visibilityExcludeRoots))
            {
                AddUniqueLight(excludedLights, excludedLightSet, light);
                continue;
            }

            if (IsUnderAnyRoomRoot(light.transform, roomRoots))
            {
                continue;
            }

            if (IsUnderAnyRoot(light.transform, manager.activeToggleRoots))
            {
                continue;
            }

            if (IsExcluded(light.transform, manager.nonRoomRoots))
            {
                controlledLights.Add(light);
                nonRoomLights++;
            }
            else
            {
                AddUniqueLight(excludedLights, excludedLightSet, light);
            }
        }

        if (manager.controlTerrains)
        {
            Terrain[] terrains = CollectCandidateTerrains(manager);
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                if (!terrain.gameObject.activeInHierarchy || !terrain.gameObject.activeSelf)
                {
                    continue;
                }

                if (IsExcluded(terrain.transform, manager.visibilityExcludeRoots))
                {
                    continue;
                }

                if (IsUnderAnyRoomRoot(terrain.transform, roomRoots))
                {
                    continue;
                }

                if (IsExcluded(terrain.transform, manager.nonRoomRoots))
                {
                    controlledTerrainObjects.Add(terrain.gameObject);
                }
            }
        }

        manager.controlledRenderers = controlled.ToArray();
        manager.controlledRendererRoomIndices = roomIndices.ToArray();
        manager.excludedRenderers = excluded.ToArray();
        manager.roomRenderers = roomRendererList.ToArray();
        manager.roomRendererRoomIndices = roomRendererRoomIndices.ToArray();
        manager.controlledCanvases = controlledCanvases.ToArray();
        manager.excludedCanvases = excludedCanvases.ToArray();
        manager.roomCanvases = roomCanvasList.ToArray();
        manager.roomCanvasRoomIndices = roomCanvasRoomIndices.ToArray();
        manager.controlledLights = controlledLights.ToArray();
        manager.excludedLights = excludedLights.ToArray();
        manager.roomLights = roomLightList.ToArray();
        manager.roomLightRoomIndices = roomLightRoomIndices.ToArray();
        manager.controlledTerrainObjects = controlledTerrainObjects.ToArray();
        manager.controlledActiveObjects = controlledActiveObjects.ToArray();
        manager.roomActiveObjects = roomActiveObjects.ToArray();
        manager.roomActiveObjectRoomIndices = roomActiveObjectRoomIndices.ToArray();
        manager.roomRendererCounts = roomRendererCounts;
        manager.roomTotalRendererCounts = roomTotalRendererCounts;
        manager.lastCollectedRendererCount = totalRoomRenderers + nonRoomRenderers;
        manager.lastControlledRendererCount = controlled.Count;
        manager.lastExcludedRendererCount = excluded.Count;
        manager.lastGeneratedBoundingBoxCount = manager.GetValidBoundingBoxCount();

        report.AppendLine("Collected Renderers: " + (totalRoomRenderers + nonRoomRenderers));
        report.AppendLine("Room Renderers: " + totalRoomRenderers);
        report.AppendLine("Non-Room Renderers: " + controlled.Count);
        report.AppendLine("Collected Canvases: " + (totalRoomCanvases + nonRoomCanvases));
        report.AppendLine("Room Canvases: " + totalRoomCanvases);
        report.AppendLine("Non-Room Canvases: " + controlledCanvases.Count);
        report.AppendLine("Always Visible Canvases: " + excludedCanvases.Count);
        report.AppendLine("Collected Lights: " + (totalRoomLights + nonRoomLights));
        report.AppendLine("Room Lights: " + totalRoomLights);
        report.AppendLine("Non-Room Lights: " + controlledLights.Count);
        report.AppendLine("Always Visible Lights: " + excludedLights.Count);
        if (manager.controlTerrains)
        {
            report.AppendLine("Non-Room Terrains: " + controlledTerrainObjects.Count);
        }
        report.AppendLine("Always Visible Renderers: " + excluded.Count);
        AppendActiveToggleConflictWarnings(manager, report);

        ApplyManagerChanges(manager);
        if (overwriteReport)
        {
            WriteReport(manager, report.ToString());
        }
        else
        {
            manager.lastBuildReport = report.ToString();
            ApplyManagerChanges(manager);
        }
    }

    private void ValidateConfiguration(RoomVisibilityManager manager)
    {
        ValidateConfiguration(manager, new StringBuilder(), true);
    }

    private void ValidateConfiguration(RoomVisibilityManager manager, StringBuilder report, bool writeReport)
    {
        if (report == null)
        {
            report = new StringBuilder();
        }

        bool valid = ValidateRequiredSetup(manager, report);
        AppendMultipleActiveManagerWarnings(manager, report, false);

        if (manager.detectedRoomRoots != null)
        {
            bool hasNonRoomRenderer = manager.lastControlledRendererCount > 0;
            bool hasNonRoomCanvas = manager.controlledCanvases != null && manager.controlledCanvases.Length > 0;
            bool hasNonRoomLight = manager.controlledLights != null && manager.controlledLights.Length > 0;
            bool hasNonRoomTerrain = manager.controlledTerrainObjects != null && manager.controlledTerrainObjects.Length > 0;
            for (int i = 0; i < manager.detectedRoomRoots.Length; i++)
            {
                GameObject roomRoot = manager.detectedRoomRoots[i];
                if (roomRoot == null)
                {
                    continue;
                }

                bool hasBoundingBox = manager.roomBoundingBoxes != null && i < manager.roomBoundingBoxes.Length && manager.roomBoundingBoxes[i] != null;
                bool hasRendererStats = manager.roomRendererCounts != null && i < manager.roomRendererCounts.Length;

                if (!hasBoundingBox)
                {
                    report.AppendLine("Warning: " + roomRoot.name + " のバウンディングボックスがありません。");
                }
            }

            if (!hasNonRoomRenderer && !hasNonRoomCanvas && !hasNonRoomLight && !hasNonRoomTerrain)
            {
                report.AppendLine("Warning: 非表示対象 Renderer / Canvas / Light / Terrain が見つかっていません。");
            }

            report.AppendLine("Canvas 非表示対象数: " + (manager.controlledCanvases == null ? 0 : manager.controlledCanvases.Length));
            report.AppendLine("Light 非表示対象数: " + (manager.controlledLights == null ? 0 : manager.controlledLights.Length));

            if (manager.controlTerrains)
            {
                report.AppendLine("Terrain 非表示対象数: " + (manager.controlledTerrainObjects == null ? 0 : manager.controlledTerrainObjects.Length));

                if (!hasNonRoomTerrain)
                {
                    report.AppendLine("Warning: 非表示対象 Terrain が見つかっていません。");
                }
            }
        }

        AppendActiveToggleConflictWarnings(manager, report);

        if (valid && report.Length == 0)
        {
            report.AppendLine("Validation OK");
            report.AppendLine("Current Room: " + manager.GetCurrentRoomName());
            report.AppendLine("Configured Rooms: " + manager.GetConfiguredRoomCount());
            report.AppendLine("Valid Bounding Boxes: " + manager.GetValidBoundingBoxCount());
        }

        if (writeReport)
        {
            WriteReport(manager, report.ToString());
        }
        else
        {
            manager.lastBuildReport = report.ToString();
            ApplyManagerChanges(manager);
        }
    }

    private void RunSetup(RoomVisibilityManager manager)
    {
        StringBuilder report = new StringBuilder();
        if (!ValidateRequiredSetup(manager, report))
        {
            WriteReport(manager, report.ToString());
            return;
        }

        AppendMultipleActiveManagerWarnings(manager, report, true);
        GenerateBoundingBoxes(manager, report, false);
        ValidateConfiguration(manager, report, true);
    }

    private static bool ValidateRequiredSetup(RoomVisibilityManager manager, StringBuilder report)
    {
        bool valid = true;

        GameObject[] roomRoots = BuildDetectedRooms(manager);
        if (roomRoots.Length == 0)
        {
            report.AppendLine("Error: 部屋ルートオブジェクトリストが空です。");
            valid = false;
        }

        if (manager.checkInterval <= 0f)
        {
            report.AppendLine("Error: 判定間隔は 0 より大きくしてください。");
            valid = false;
        }

        if (manager.startupDelaySeconds < 0f)
        {
            report.AppendLine("Error: 開始待機秒数は 0 以上にしてください。");
            valid = false;
        }

        if (manager.boundsHysteresisMargin < 0f)
        {
            report.AppendLine("Error: 判定ヒステリシスは 0 以上にしてください。");
            valid = false;
        }

        if (manager.roomRoots != null)
        {
            for (int i = 0; i < manager.roomRoots.Length; i++)
            {
                GameObject roomRoot = manager.roomRoots[i];
                if (roomRoot != null && IsEditorOnlyObject(roomRoot.transform))
                {
                    report.AppendLine("Warning: " + roomRoot.name + " は EditorOnly 配下のため Runtime では部屋として扱われません。");
                }
            }
        }

        return valid;
    }

    private static void AppendMultipleActiveManagerWarnings(RoomVisibilityManager manager, StringBuilder report, bool showDialog)
    {
        if (manager == null || report == null || !manager.gameObject.scene.IsValid())
        {
            return;
        }

        List<RoomVisibilityManager> activeManagers = CollectActiveManagersInSameScene(manager);
        if (activeManagers.Count <= 1)
        {
            return;
        }

        report.AppendLine("Warning: 同一シーン内に有効な RoomVisibilityManager が複数あります。複数配置は非推奨です。");
        for (int i = 0; i < activeManagers.Count; i++)
        {
            RoomVisibilityManager activeManager = activeManagers[i];
            if (activeManager == null)
            {
                continue;
            }

            report.AppendLine("Warning: Active Manager: " + activeManager.gameObject.name);
        }

        if (!showDialog)
        {
            return;
        }

        StringBuilder dialogMessage = new StringBuilder();
        dialogMessage.AppendLine("有効な RoomVisibilityManager が複数あります。");
        dialogMessage.AppendLine();
        for (int i = 0; i < activeManagers.Count; i++)
        {
            RoomVisibilityManager activeManager = activeManagers[i];
            if (activeManager == null)
            {
                continue;
            }

            dialogMessage.AppendLine("・" + activeManager.gameObject.name);
        }

        dialogMessage.AppendLine();
        dialogMessage.AppendLine("同一シーン内で複数の RoomVisibilityManager を有効にすると、正しく動作しません。");
        EditorUtility.DisplayDialog("Room Visibility Warning", dialogMessage.ToString(), "OK");
    }

    private static List<RoomVisibilityManager> CollectActiveManagersInSameScene(RoomVisibilityManager manager)
    {
        List<RoomVisibilityManager> activeManagers = new List<RoomVisibilityManager>();
        RoomVisibilityManager[] allManagers = UnityEngine.Object.FindObjectsOfType<RoomVisibilityManager>(true);
        for (int i = 0; i < allManagers.Length; i++)
        {
            RoomVisibilityManager candidate = allManagers[i];
            if (!IsActiveComparableManager(manager, candidate))
            {
                continue;
            }

            activeManagers.Add(candidate);
        }

        return activeManagers;
    }

    private void EnsureRoomVisibilityMatrix(RoomVisibilityManager manager)
    {
        if (manager == null)
        {
            return;
        }

        GameObject[] rooms = BuildDetectedRooms(manager);
        int roomCount = rooms.Length;
        int targetCount = roomCount + 1;
        int expectedLength = targetCount * targetCount;
        if (_roomHiddenMatrix.arraySize == expectedLength)
        {
            return;
        }

        bool[] oldMatrix = manager.roomHiddenMatrix;
        int oldRoomCount = manager.detectedRoomRoots == null ? 0 : manager.detectedRoomRoots.Length;
        int oldTargetCount = oldRoomCount + 1;
        bool[] newMatrix = BuildDefaultRoomHiddenMatrix(roomCount);

        if (oldMatrix != null && oldMatrix.Length == oldTargetCount * oldTargetCount)
        {
            int copyTargetCount = Mathf.Min(oldTargetCount, targetCount);
            for (int stateIndex = 0; stateIndex < copyTargetCount; stateIndex++)
            {
                for (int targetIndex = 0; targetIndex < copyTargetCount; targetIndex++)
                {
                    int oldIndex = stateIndex * oldTargetCount + targetIndex;
                    int newIndex = stateIndex * targetCount + targetIndex;
                    newMatrix[newIndex] = oldMatrix[oldIndex];
                }
            }
        }

        for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
        {
            newMatrix[targetIndex * targetCount + targetIndex] = true;
        }

        Undo.RegisterCompleteObjectUndo(manager, "Resize Room Visibility Matrix");
        manager.roomHiddenMatrix = newMatrix;
        EditorUtility.SetDirty(manager);

        serializedObject.Update();
    }

    private static bool[] BuildDefaultRoomHiddenMatrix(int roomCount)
    {
        int targetCount = roomCount + 1;
        bool[] matrix = new bool[targetCount * targetCount];
        for (int stateIndex = 0; stateIndex < targetCount; stateIndex++)
        {
            for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
            {
                bool visible = stateIndex == targetIndex;
                matrix[stateIndex * targetCount + targetIndex] = visible;
            }
        }

        return matrix;
    }

    private static string GetStateLabel(GameObject[] rooms, int stateIndex)
    {
        if (stateIndex == 0)
        {
            return "室外";
        }

        int roomIndex = stateIndex - 1;
        if (rooms == null || roomIndex < 0 || roomIndex >= rooms.Length)
        {
            return "部屋 " + stateIndex;
        }

        GameObject room = rooms[roomIndex];
        return room == null ? ("部屋 " + stateIndex) : room.name;
    }

    private static bool TryCollectRoomBounds(GameObject roomRoot, GameObject[] ignoreRoots, bool includeInactive, out Bounds bounds, StringBuilder report)
    {
        Renderer[] renderers = roomRoot.GetComponentsInChildren<Renderer>(includeInactive);
        bool hasBounds = false;
        bounds = new Bounds(roomRoot.transform.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (IsGeneratedBoundingBoxObject(renderer.transform))
            {
                continue;
            }

            if (IsEditorOnlyObject(renderer.transform))
            {
                continue;
            }

            if (IsUnderAnyRoot(renderer.transform, ignoreRoots))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = roomRoot.GetComponentsInChildren<Collider>(includeInactive);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (IsGeneratedBoundingBoxObject(collider.transform))
            {
                continue;
            }

            if (IsEditorOnlyObject(collider.transform))
            {
                continue;
            }

            if (IsUnderAnyRoot(collider.transform, ignoreRoots))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
        {
            report.AppendLine("Warning: " + roomRoot.name + " 配下に Renderer / Collider が見つかりません。");
        }

        return hasBounds;
    }

    private static void ConfigureBoundingBox(BoxCollider boundingBox, GameObject roomRoot, Bounds bounds, float margin)
    {
        if (boundingBox == null)
        {
            return;
        }

        float safeMargin = Mathf.Max(0f, margin);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 center = new Vector3(
            (min.x + max.x) * 0.5f,
            (min.y + max.y) * 0.5f,
            (min.z + max.z) * 0.5f
        );
        Vector3 size = new Vector3(
            (max.x - min.x) + safeMargin * 2f,
            (max.y - min.y) + safeMargin * 2f,
            (max.z - min.z) + safeMargin * 2f
        );

        Transform boundingBoxTransform = boundingBox.transform;
        boundingBoxTransform.position = center;
        boundingBoxTransform.rotation = Quaternion.identity;
        boundingBoxTransform.localScale = GetCompensatedLocalScale(boundingBoxTransform.parent);

        boundingBox.center = Vector3.zero;
        boundingBox.size = size;
        boundingBox.isTrigger = true;

        RoomVisibilityBoundingBox helper = boundingBox.GetComponent<RoomVisibilityBoundingBox>();
        if (helper != null)
        {
            helper.roomRoot = roomRoot;
        }

        EditorUtility.SetDirty(boundingBox);
        if (helper != null)
        {
            EditorUtility.SetDirty(helper);
        }
    }

    private static Vector3 GetCompensatedLocalScale(Transform parent)
    {
        if (parent == null)
        {
            return Vector3.one;
        }

        Vector3 lossyScale = parent.lossyScale;
        return new Vector3(
            SafeInverseScale(lossyScale.x),
            SafeInverseScale(lossyScale.y),
            SafeInverseScale(lossyScale.z)
        );
    }

    private static float SafeInverseScale(float value)
    {
        if (Mathf.Abs(value) < 0.0001f)
        {
            return 1f;
        }

        return 1f / value;
    }

    private static string BuildBoundingBoxName(RoomVisibilityManager manager, GameObject roomRoot, int index)
    {
        string prefix = string.IsNullOrEmpty(manager.boundingBoxNamePrefix) ? "RoomBoundingBox_" : manager.boundingBoxNamePrefix;
        string roomName = roomRoot == null ? ("Room_" + index) : roomRoot.name;
        return prefix + roomName;
    }

    private static bool IsExcluded(Transform target, GameObject[] excludedRoots)
    {
        return IsUnderAnyRoot(target, excludedRoots);
    }

    private static bool IsDescendantOrSame(Transform ancestor, Transform target)
    {
        if (ancestor == null || target == null)
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static int GetRelativeDepth(Transform ancestor, Transform target)
    {
        int depth = 0;
        Transform current = target;
        while (current != null)
        {
            if (current == ancestor)
            {
                return depth;
            }

            current = current.parent;
            depth++;
        }

        return -1;
    }

    private static void WriteReport(RoomVisibilityManager manager, string report)
    {
        manager.lastBuildReport = string.IsNullOrEmpty(report) ? "(No messages)" : report;
        ApplyManagerChanges(manager);
    }

    private static void ApplyManagerChanges(RoomVisibilityManager manager)
    {
        EditorUtility.SetDirty(manager);
    }

    private static GameObject[] BuildDetectedRooms(RoomVisibilityManager manager)
    {
        if (manager == null || manager.roomRoots == null || manager.roomRoots.Length == 0)
        {
            return new GameObject[0];
        }

        List<GameObject> rooms = new List<GameObject>();
        for (int i = 0; i < manager.roomRoots.Length; i++)
        {
            GameObject roomRoot = manager.roomRoots[i];
            if (roomRoot != null && !IsEditorOnlyObject(roomRoot.transform))
            {
                rooms.Add(roomRoot);
            }
        }

        return rooms.ToArray();
    }

    private static Renderer[] CollectCandidateRenderers(RoomVisibilityManager manager)
    {
        HashSet<Renderer> rendererSet = new HashSet<Renderer>();

        if (manager.nonRoomRoots != null)
        {
            for (int i = 0; i < manager.nonRoomRoots.Length; i++)
            {
                GameObject nonRoomRoot = manager.nonRoomRoots[i];
                if (nonRoomRoot == null)
                {
                    continue;
                }

                Renderer[] extraRenderers = nonRoomRoot.GetComponentsInChildren<Renderer>(manager.includeInactiveRenderers);
                for (int j = 0; j < extraRenderers.Length; j++)
                {
                    if (extraRenderers[j] != null)
                    {
                        rendererSet.Add(extraRenderers[j]);
                    }
                }
            }
        }

        Renderer[] result = new Renderer[rendererSet.Count];
        rendererSet.CopyTo(result);
        return result;
    }

    private static Canvas[] CollectCandidateCanvases(RoomVisibilityManager manager)
    {
        HashSet<Canvas> canvasSet = new HashSet<Canvas>();

        if (manager != null && manager.nonRoomRoots != null)
        {
            for (int i = 0; i < manager.nonRoomRoots.Length; i++)
            {
                GameObject nonRoomRoot = manager.nonRoomRoots[i];
                if (nonRoomRoot == null)
                {
                    continue;
                }

                Canvas[] canvases = nonRoomRoot.GetComponentsInChildren<Canvas>(manager.includeInactiveRenderers);
                for (int j = 0; j < canvases.Length; j++)
                {
                    if (canvases[j] != null)
                    {
                        canvasSet.Add(canvases[j]);
                    }
                }
            }
        }

        Canvas[] result = new Canvas[canvasSet.Count];
        canvasSet.CopyTo(result);
        return result;
    }

    private static Light[] CollectCandidateLights(RoomVisibilityManager manager)
    {
        HashSet<Light> lightSet = new HashSet<Light>();

        if (manager != null && manager.nonRoomRoots != null)
        {
            for (int i = 0; i < manager.nonRoomRoots.Length; i++)
            {
                GameObject nonRoomRoot = manager.nonRoomRoots[i];
                if (nonRoomRoot == null)
                {
                    continue;
                }

                Light[] lights = nonRoomRoot.GetComponentsInChildren<Light>(manager.includeInactiveRenderers);
                for (int j = 0; j < lights.Length; j++)
                {
                    if (lights[j] != null)
                    {
                        lightSet.Add(lights[j]);
                    }
                }
            }
        }

        Light[] result = new Light[lightSet.Count];
        lightSet.CopyTo(result);
        return result;
    }

    private static Terrain[] CollectCandidateTerrains(RoomVisibilityManager manager)
    {
        HashSet<Terrain> terrainSet = new HashSet<Terrain>();

        if (manager != null && manager.nonRoomRoots != null)
        {
            for (int i = 0; i < manager.nonRoomRoots.Length; i++)
            {
                GameObject nonRoomRoot = manager.nonRoomRoots[i];
                if (nonRoomRoot == null)
                {
                    continue;
                }

                Terrain[] terrains = nonRoomRoot.GetComponentsInChildren<Terrain>(true);
                for (int j = 0; j < terrains.Length; j++)
                {
                    if (terrains[j] != null)
                    {
                        terrainSet.Add(terrains[j]);
                    }
                }
            }
        }

        Terrain[] result = new Terrain[terrainSet.Count];
        terrainSet.CopyTo(result);
        return result;
    }

    private static bool IsUnderAnyRoomRoot(Transform target, GameObject[] roomRoots)
    {
        return IsUnderAnyRoot(target, roomRoots);
    }

    private static bool IsUnderAnyRoot(Transform target, GameObject[] roots)
    {
        if (target == null || roots == null)
        {
            return false;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && IsDescendantOrSame(root.transform, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedBoundingBoxObject(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current.GetComponent<RoomVisibilityBoundingBox>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsEligibleForVisibilityControl(Renderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (!renderer.enabled)
        {
            return false;
        }

        GameObject rendererObject = renderer.gameObject;
        if (rendererObject == null)
        {
            return false;
        }

        if (!rendererObject.activeSelf)
        {
            return false;
        }

        if (!rendererObject.activeInHierarchy)
        {
            return false;
        }

        if (IsEditorOnlyObject(renderer.transform))
        {
            return false;
        }

        return true;
    }

    private static bool IsEligibleForVisibilityControl(Canvas canvas)
    {
        if (canvas == null)
        {
            return false;
        }

        if (!canvas.enabled)
        {
            return false;
        }

        GameObject canvasObject = canvas.gameObject;
        if (canvasObject == null)
        {
            return false;
        }

        if (!canvasObject.activeSelf)
        {
            return false;
        }

        if (!canvasObject.activeInHierarchy)
        {
            return false;
        }

        if (IsEditorOnlyObject(canvas.transform))
        {
            return false;
        }

        return true;
    }

    private static bool IsEligibleForVisibilityControl(Light light)
    {
        if (light == null)
        {
            return false;
        }

        if (!light.enabled)
        {
            return false;
        }

        GameObject lightObject = light.gameObject;
        if (lightObject == null)
        {
            return false;
        }

        if (!lightObject.activeSelf)
        {
            return false;
        }

        if (!lightObject.activeInHierarchy)
        {
            return false;
        }

        if (IsEditorOnlyObject(light.transform))
        {
            return false;
        }

        return true;
    }

    private static void CollectActiveToggleObjects(
        RoomVisibilityManager manager,
        GameObject[] roomRoots,
        List<GameObject> controlledActiveObjects,
        List<GameObject> roomActiveObjects,
        List<int> roomActiveObjectRoomIndices,
        HashSet<GameObject> activeObjectSet)
    {
        if (manager == null || manager.activeToggleRoots == null)
        {
            return;
        }

        for (int i = 0; i < manager.activeToggleRoots.Length; i++)
        {
            GameObject target = manager.activeToggleRoots[i];
            if (target == null)
            {
                continue;
            }

            if (IsUnderAnyRoot(target.transform, manager.visibilityExcludeRoots))
            {
                continue;
            }

            if (IsEditorOnlyObject(target.transform))
            {
                continue;
            }

            if (HasAncestorInSet(target.transform, activeObjectSet))
            {
                continue;
            }

            if (!activeObjectSet.Add(target))
            {
                continue;
            }

            int roomIndex = GetContainingRoomIndex(target.transform, roomRoots);
            if (roomIndex >= 0)
            {
                roomActiveObjects.Add(target);
                roomActiveObjectRoomIndices.Add(roomIndex);
                continue;
            }

            if (IsUnderAnyRoot(target.transform, manager.nonRoomRoots))
            {
                controlledActiveObjects.Add(target);
            }
        }
    }

    private static int GetContainingRoomIndex(Transform target, GameObject[] roomRoots)
    {
        if (target == null || roomRoots == null)
        {
            return -1;
        }

        for (int i = 0; i < roomRoots.Length; i++)
        {
            GameObject roomRoot = roomRoots[i];
            if (roomRoot != null && IsDescendantOrSame(roomRoot.transform, target))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HasAncestorInSet(Transform target, HashSet<GameObject> roots)
    {
        if (target == null || roots == null)
        {
            return false;
        }

        Transform current = target.parent;
        while (current != null)
        {
            if (roots.Contains(current.gameObject))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void AppendActiveToggleConflictWarnings(RoomVisibilityManager manager, StringBuilder report)
    {
        if (manager == null || report == null || manager.visibilityExcludeRoots == null || manager.activeToggleRoots == null)
        {
            return;
        }

        int warningCount = 0;
        for (int excludeIndex = 0; excludeIndex < manager.visibilityExcludeRoots.Length; excludeIndex++)
        {
            GameObject excludeRoot = manager.visibilityExcludeRoots[excludeIndex];
            if (excludeRoot == null)
            {
                continue;
            }

            for (int activeIndex = 0; activeIndex < manager.activeToggleRoots.Length; activeIndex++)
            {
                GameObject activeRoot = manager.activeToggleRoots[activeIndex];
                if (activeRoot == null)
                {
                    continue;
                }

                if (!IsDescendantOrSame(activeRoot.transform, excludeRoot.transform))
                {
                    continue;
                }

                report.AppendLine(
                    "Warning: 表示切替対象外オブジェクト " + excludeRoot.name +
                    " が GameObject 有効状態で切り替えるオブジェクト " + activeRoot.name +
                    " の子孫です。親の SetActive に巻き込まれて非アクティブになります。");
                warningCount++;
                break;
            }
        }

        if (warningCount > 0)
        {
            report.AppendLine("Warning: 常時表示したい除外対象は、GameObject 有効状態で切り替える親の外へ移動してください。");
        }
    }

    private static void ShowBoundingBoxOverlapDialogIfNeeded(RoomVisibilityManager manager, BoxCollider[] generatedBoundingBoxes, StringBuilder report)
    {
        if (manager == null || generatedBoundingBoxes == null || report == null)
        {
            return;
        }

        List<string> ownOverlapItems = new List<string>();
        HashSet<string> ownOverlapKeys = new HashSet<string>();
        CollectOwnManagerBoundingBoxOverlaps(generatedBoundingBoxes, report, ownOverlapItems, ownOverlapKeys);

        List<string> otherManagerItems = new List<string>();
        HashSet<string> otherManagerKeys = new HashSet<string>();
        RoomVisibilityManager[] allManagers = UnityEngine.Object.FindObjectsOfType<RoomVisibilityManager>(true);
        for (int i = 0; i < generatedBoundingBoxes.Length; i++)
        {
            BoxCollider ownBoundingBox = generatedBoundingBoxes[i];
            if (ownBoundingBox == null)
            {
                continue;
            }

            Bounds ownBounds = ownBoundingBox.bounds;
            for (int managerIndex = 0; managerIndex < allManagers.Length; managerIndex++)
            {
                RoomVisibilityManager otherManager = allManagers[managerIndex];
                if (!ShouldCompareWithOtherManager(manager, otherManager))
                {
                    continue;
                }

                for (int otherBoundingBoxIndex = 0; otherBoundingBoxIndex < otherManager.roomBoundingBoxes.Length; otherBoundingBoxIndex++)
                {
                    BoxCollider otherBoundingBox = otherManager.roomBoundingBoxes[otherBoundingBoxIndex];
                    if (otherBoundingBox == null)
                    {
                        continue;
                    }

                    if (ownBounds.Intersects(otherBoundingBox.bounds))
                    {
                        string ownRoomName = ownBoundingBox.name;
                        string otherRoomName = otherBoundingBox.name;
                        string warningMessage =
                            "Warning: " + ownRoomName + " が他オブジェクトの RoomVisibilityManager (" +
                            otherManager.gameObject.name + ") の " + otherRoomName + " と重なっています。";
                        report.AppendLine(warningMessage);

                        string overlapKey = otherManager.gameObject.scene.path + "|" + otherManager.gameObject.name + "|" + ownRoomName + "|" + otherRoomName;
                        if (otherManagerKeys.Add(overlapKey))
                        {
                            otherManagerItems.Add("・" + ownRoomName + " と " + otherManager.gameObject.name + " / " + otherRoomName);
                        }
                    }
                }
            }
        }

        if (ownOverlapItems.Count > 0 || otherManagerItems.Count > 0)
        {
            StringBuilder dialogMessage = new StringBuilder();
            dialogMessage.AppendLine("重なっている部屋があります。");
            dialogMessage.AppendLine();

            if (ownOverlapItems.Count > 0)
            {
                for (int i = 0; i < ownOverlapItems.Count; i++)
                {
                    dialogMessage.AppendLine(ownOverlapItems[i]);
                }
            }

            if (otherManagerItems.Count > 0)
            {
                if (ownOverlapItems.Count > 0)
                {
                    dialogMessage.AppendLine();
                }

                dialogMessage.AppendLine("別の RoomVisibilityManager との重なりもあります。");
                for (int i = 0; i < otherManagerItems.Count; i++)
                {
                    dialogMessage.AppendLine(otherManagerItems[i]);
                }
            }

            dialogMessage.AppendLine();
            dialogMessage.AppendLine("重なっているとき正しく動作しません。");
            dialogMessage.AppendLine("重なっている複数の部屋を1つの部屋になるように設定してください。");

            EditorUtility.DisplayDialog("Room Visibility Warning", dialogMessage.ToString(), "OK");
        }
    }

    private static void CollectOwnManagerBoundingBoxOverlaps(
        BoxCollider[] generatedBoundingBoxes,
        StringBuilder report,
        List<string> overlapItems,
        HashSet<string> overlapKeys)
    {
        for (int i = 0; i < generatedBoundingBoxes.Length; i++)
        {
            BoxCollider firstBoundingBox = generatedBoundingBoxes[i];
            if (firstBoundingBox == null)
            {
                continue;
            }

            Bounds firstBounds = firstBoundingBox.bounds;
            for (int j = i + 1; j < generatedBoundingBoxes.Length; j++)
            {
                BoxCollider secondBoundingBox = generatedBoundingBoxes[j];
                if (secondBoundingBox == null || !firstBounds.Intersects(secondBoundingBox.bounds))
                {
                    continue;
                }

                string firstRoomName = firstBoundingBox.name;
                string secondRoomName = secondBoundingBox.name;
                string warningMessage =
                    "Warning: " + firstRoomName + " が同じ RoomVisibilityManager の " + secondRoomName + " と重なっています。";
                report.AppendLine(warningMessage);

                string overlapKey = firstRoomName + "|" + secondRoomName;
                if (overlapKeys.Add(overlapKey))
                {
                    overlapItems.Add("・" + firstRoomName + " と " + secondRoomName);
                }
            }
        }
    }

    private static bool ShouldCompareWithOtherManager(RoomVisibilityManager manager, RoomVisibilityManager otherManager)
    {
        if (!IsActiveComparableManager(manager, otherManager) || otherManager == manager || otherManager.roomBoundingBoxes == null)
        {
            return false;
        }

        PrefabStage managerPrefabStage = PrefabStageUtility.GetPrefabStage(manager.gameObject);
        PrefabStage otherPrefabStage = PrefabStageUtility.GetPrefabStage(otherManager.gameObject);
        return managerPrefabStage == otherPrefabStage;
    }

    private static bool IsActiveComparableManager(RoomVisibilityManager manager, RoomVisibilityManager otherManager)
    {
        if (manager == null || otherManager == null)
        {
            return false;
        }

        if (!manager.gameObject.scene.IsValid() || !otherManager.gameObject.scene.IsValid())
        {
            return false;
        }

        if (manager.gameObject.scene != otherManager.gameObject.scene)
        {
            return false;
        }

        if (!otherManager.enabled || !otherManager.gameObject.activeInHierarchy || IsEditorOnlyObject(otherManager.transform))
        {
            return false;
        }

        return true;
    }

    private static bool IsEditorOnlyObject(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag("EditorOnly"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void PreserveRendererStates(RoomVisibilityManager manager, Action action)
    {
        Renderer[] renderers = CollectAllRelevantRenderers(manager);
        bool[] enabledStates = new bool[renderers.Length];
        List<Renderer> validRenderers = new List<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            enabledStates[i] = renderer != null && renderer.enabled;
            if (renderer != null)
            {
                validRenderers.Add(renderer);
            }
        }

        if (validRenderers.Count > 0)
        {
            Undo.RecordObjects(validRenderers.ToArray(), "Preserve Renderer Visibility");
        }

        try
        {
            action();
        }
        finally
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.enabled != enabledStates[i])
                {
                    renderer.enabled = enabledStates[i];
                    EditorUtility.SetDirty(renderer);
                }
            }
        }
    }

    private static Renderer[] CollectAllRelevantRenderers(RoomVisibilityManager manager)
    {
        HashSet<Renderer> rendererSet = new HashSet<Renderer>();

        if (manager != null && manager.roomRoots != null)
        {
            for (int i = 0; i < manager.roomRoots.Length; i++)
            {
                AddRenderersUnderRoot(rendererSet, manager.roomRoots[i], true);
            }
        }

        if (manager != null && manager.nonRoomRoots != null)
        {
            for (int i = 0; i < manager.nonRoomRoots.Length; i++)
            {
                AddRenderersUnderRoot(rendererSet, manager.nonRoomRoots[i], true);
            }
        }

        if (manager != null && manager.boundsIgnoreRoots != null)
        {
            for (int i = 0; i < manager.boundsIgnoreRoots.Length; i++)
            {
                AddRenderersUnderRoot(rendererSet, manager.boundsIgnoreRoots[i], true);
            }
        }

        if (manager != null && manager.visibilityExcludeRoots != null)
        {
            for (int i = 0; i < manager.visibilityExcludeRoots.Length; i++)
            {
                AddRenderersUnderRoot(rendererSet, manager.visibilityExcludeRoots[i], true);
            }
        }

        Renderer[] result = new Renderer[rendererSet.Count];
        rendererSet.CopyTo(result);
        return result;
    }

    private static void AddRenderersUnderRoot(HashSet<Renderer> rendererSet, GameObject root, bool includeInactive)
    {
        if (rendererSet == null || root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                rendererSet.Add(renderers[i]);
            }
        }
    }

    private static void AddUniqueRenderer(List<Renderer> renderers, HashSet<Renderer> rendererSet, Renderer renderer)
    {
        if (renderers == null || rendererSet == null || renderer == null)
        {
            return;
        }

        if (rendererSet.Add(renderer))
        {
            renderers.Add(renderer);
        }
    }

    private static void AddUniqueCanvas(List<Canvas> canvases, HashSet<Canvas> canvasSet, Canvas canvas)
    {
        if (canvases == null || canvasSet == null || canvas == null)
        {
            return;
        }

        if (canvasSet.Add(canvas))
        {
            canvases.Add(canvas);
        }
    }

    private static void AddUniqueLight(List<Light> lights, HashSet<Light> lightSet, Light light)
    {
        if (lights == null || lightSet == null || light == null)
        {
            return;
        }

        if (lightSet.Add(light))
        {
            lights.Add(light);
        }
    }

    private static void SplitReportMessages(string report, out string errors, out string warnings, out string others)
    {
        StringBuilder errorBuilder = new StringBuilder();
        StringBuilder warningBuilder = new StringBuilder();
        StringBuilder otherBuilder = new StringBuilder();

        if (!string.IsNullOrEmpty(report))
        {
            string[] lines = report.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Error: "))
                {
                    AppendReportLine(errorBuilder, line);
                    continue;
                }

                if (line.StartsWith("Warning: "))
                {
                    AppendReportLine(warningBuilder, line);
                    continue;
                }

                AppendReportLine(otherBuilder, line);
            }
        }

        errors = errorBuilder.ToString();
        warnings = warningBuilder.ToString();
        others = otherBuilder.ToString();
    }

    private static void AppendReportLine(StringBuilder builder, string line)
    {
        if (builder == null || string.IsNullOrEmpty(line))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
    }
}
#endif
