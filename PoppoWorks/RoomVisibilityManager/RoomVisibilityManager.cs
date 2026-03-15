using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;

public enum RoomVisibilityPlayerPoint
{
    AvatarRoot = 0,
    Head = 1
}

public enum RoomVisibilityOutsideRoomBehaviour
{
    ShowAll = 0,
    HideControlledOnly = 1
}

public class RoomVisibilityManager : UdonSharpBehaviour
{
    [Header("1. User Settings / Basic")]
    [Tooltip("部屋として扱いたい GameObject のルートを並べます。各要素の子孫全体を 1 部屋として扱います。部屋の増減や親子構造を変更したあとは、セットアップ実行でバウンディングボックスと制御対象を再生成してください。")]
    public GameObject[] roomRoots;

    [Tooltip("部屋そのものではないが、この仕組みで表示・非表示を切り替えたいオブジェクトのルートを指定します。主に外景、遠景、共用通路、部屋の外にある装飾などを入れます。各部屋に入ったときの ON/OFF は部屋表示制御表の『室外』列で決まります。")]
    public GameObject[] nonRoomRoots;

    [Tooltip("このリストに入れたオブジェクト配下は、roomRoots / nonRoomRoots 配下にあっても表示切替の対象から外します。常に表示したい床、ギミック、補助コライダー、UI などに使います。Renderer / Canvas / Light / Terrain に対応しますが、GameObject ごと非アクティブ化される親の子にある場合は巻き込まれて消えます。")]
    public GameObject[] visibilityExcludeRoots;

    [Header("1. User Settings / Detection")]
    [Tooltip("プレーヤーが今どの部屋にいるかを判定するときの基準点です。通常は AvatarRoot を使います。頭だけが先に部屋へ入る演出や、高低差の影響を強く受けたい場合は Head を選びます。")]
    public RoomVisibilityPlayerPoint playerPoint = RoomVisibilityPlayerPoint.AvatarRoot;

    [Tooltip("部屋判定を何秒ごとに行うかです。小さいほど追従は速くなりますが、判定回数は増えます。0 より大きい値を入れてください。")]
    public float checkInterval = 0.1f;

    [Tooltip("ワールド入場後、この秒数が経過するまで部屋判定と表示切替を開始しません。初期化中のチラつきや、入場直後の位置未確定による誤判定を避けたいときに使います。")]
    public float startupDelaySeconds = 0f;

    [Tooltip("ON のとき、Unity Console に部屋判定と表示切替の詳細ログを出します。原因調査用です。")]
    public bool enableDebugLogs = false;

    [Tooltip("部屋判定に使うバウンディングボックスの境界に追加する余裕です。部屋の境界で判定が切れやすい、出入り口でチラつく、といった場合に少し増やします。大きくしすぎると隣の部屋へ食い込みやすくなります。")]
    public float boundsMargin = 0.1f;

    [Tooltip("現在いる部屋に留まる判定へ追加する余裕です。0 より大きい値を入れると、入室時より退室時の判定が少し緩くなり、境界でのチラつきを抑えやすくなります。0 のときはヒステリシスなしです。")]
    public float boundsHysteresisMargin = 0f;

    [Tooltip("旧仕様との互換のため残っている設定です。現在の実装では使っていません。変更しても挙動は変わりません。")]
    public RoomVisibilityOutsideRoomBehaviour outsideRoomBehaviour = RoomVisibilityOutsideRoomBehaviour.ShowAll;

    [Tooltip("旧仕様との互換のため残っている設定です。現在の実装では使っていません。変更しても挙動は変わりません。")]
    public bool hideRoomsWhenOutside = false;

    [Tooltip("ON のとき、nonRoomRoots 配下にある Terrain も表示切替の対象に含めます。Terrain は細かい描画設定ではなく GameObject 単位で ON/OFF します。部屋に入ったとき外の地形ごと消したい場合に使います。")]
    public bool controlTerrains = false;

    [Tooltip("このリストに入れた親配下は、Renderer.enabled / Canvas.enabled / Light.enabled の個別制御ではなく、GameObject.SetActive で親ごと切り替えます。複数コンポーネントをまとめて消したいオブジェクト向けです。配下に『表示切替対象外』を入れていても、親が非アクティブになると一緒に消えます。")]
    public GameObject[] activeToggleRoots;

    [Tooltip("行は『プレーヤーが今いる場所』、列は『表示したい対象』です。0 列目は室外、以降は roomRoots の順です。true のセルはその位置にいるとき対象を表示し、false は非表示にします。対角成分は常に表示扱いです。")]
    public bool[] roomHiddenMatrix;

    [Header("1. User Settings / Bounding Box")]
    [Tooltip("セットアップ実行で部屋バウンディングボックスを自動生成するとき、X/Y/Z 方向へ追加する余白量です。部屋全体を少し大きめに囲いたいときに使います。大きすぎると別の部屋と重なりやすくなります。")]
    public float boundingBoxMargin = 1f;

    [Tooltip("部屋の大きさを自動計算するときに含めたくないオブジェクトのルートを指定します。極端に大きいエフェクト、QvPen、仮配置オブジェクト、遠くへ伸びる装飾などが原因でバウンディングボックスが不自然に大きくなる場合に使います。")]
    public GameObject[] boundsIgnoreRoots;

    [FormerlySerializedAs("volumeParent")]
    [Tooltip("自動生成したバウンディングボックスをぶら下げる親 Transform です。未設定ならこの RoomVisibilityManager 自身の下に生成します。シーン階層を整理したい場合だけ変更してください。")]
    public Transform boundingBoxParent;

    [FormerlySerializedAs("volumeNamePrefix")]
    [Tooltip("自動生成されるバウンディングボックス名の先頭に付ける文字列です。部屋名の前に共通プレフィックスを付けたいときに使います。")]
    public string boundingBoxNamePrefix = "RoomBoundingBox_";

    [FormerlySerializedAs("reuseExistingVolumes")]
    [Tooltip("既存のバウンディングボックスを再利用する予定の互換項目です。現在の実装では、セットアップ実行時に一度削除して作り直すため、実質的には効きません。")]
    public bool reuseExistingBoundingBoxes = true;

    [Tooltip("ON のとき、非アクティブな子オブジェクトも部屋 Bounds 計算や制御対象探索に含めます。普段は OFF でも、将来表示したいものをあらかじめ収集したい場合に使います。")]
    public bool includeInactiveRenderers = true;

    [Header("4. Derived Runtime Data")]
    [FormerlySerializedAs("roomVolumes")]
    [Tooltip("各部屋の判定に使う BoxCollider 一覧です。通常は手入力せず、セットアップ実行で生成・更新します。Runtime はこの配列をそのまま見て現在の部屋を判定します。")]
    public BoxCollider[] roomBoundingBoxes;

    [Tooltip("nonRoomRoots 配下から収集された Renderer 一覧です。部屋に入ったとき『室外』列の設定に従って enabled を切り替えます。通常はセットアップ実行で自動構築されます。")]
    public Renderer[] controlledRenderers;

    [Tooltip("旧実装との互換維持用の配列です。現在は実質固定値で使っており、通常は気にしなくて構いません。")]
    public int[] controlledRendererRoomIndices;

    [Tooltip("visibilityExcludeRoots 配下から収集された Renderer 一覧です。Runtime 中も常に表示を維持するため、毎回 enabled = true を入れます。")]
    public Renderer[] excludedRenderers;

    [Tooltip("roomRoots 配下から収集された Renderer 一覧です。現在の部屋と部屋表示制御表に従って enabled を切り替えます。")]
    public Renderer[] roomRenderers;

    [Tooltip("roomRenderers の各要素がどの部屋に属するかを表すインデックス一覧です。roomRoots の順番と対応します。")]
    public int[] roomRendererRoomIndices;

    [Tooltip("nonRoomRoots 配下から収集された Canvas 一覧です。『室外』列の設定に従って Canvas.enabled を切り替えます。")]
    public Canvas[] controlledCanvases;

    [Tooltip("visibilityExcludeRoots 配下から収集された Canvas 一覧です。常時表示扱いとして維持されます。")]
    public Canvas[] excludedCanvases;

    [Tooltip("roomRoots 配下から収集された Canvas 一覧です。現在の部屋に応じて Canvas.enabled を切り替えます。")]
    public Canvas[] roomCanvases;

    [Tooltip("roomCanvases の各要素が属する部屋インデックスです。roomRoots の並び順に対応します。")]
    public int[] roomCanvasRoomIndices;

    [Tooltip("nonRoomRoots 配下から収集された Light 一覧です。『室外』列の設定に従って Light.enabled を切り替えます。")]
    public Light[] controlledLights;

    [Tooltip("visibilityExcludeRoots 配下から収集された Light 一覧です。常に有効状態を維持したいライトがここに入ります。")]
    public Light[] excludedLights;

    [Tooltip("roomRoots 配下から収集された Light 一覧です。現在の部屋に応じて Light.enabled を切り替えます。")]
    public Light[] roomLights;

    [Tooltip("roomLights の各要素がどの部屋に属するかを示すインデックス一覧です。")]
    public int[] roomLightRoomIndices;

    [Tooltip("nonRoomRoots 配下から収集された Terrain の GameObject 一覧です。controlTerrains が ON のときだけ使われ、GameObject ごと SetActive で切り替えます。")]
    public GameObject[] controlledTerrainObjects;

    [Tooltip("nonRoomRoots 側で GameObject.SetActive により切り替える対象一覧です。activeToggleRoots 配下として収集された親オブジェクトが入ります。")]
    public GameObject[] controlledActiveObjects;

    [Tooltip("roomRoots 側で GameObject.SetActive により切り替える対象一覧です。activeToggleRoots 配下として収集された親オブジェクトが入ります。")]
    public GameObject[] roomActiveObjects;

    [Tooltip("roomActiveObjects の各要素が属する部屋インデックスです。roomRoots の並び順と対応します。")]
    public int[] roomActiveObjectRoomIndices;

    [Tooltip("この Manager が実際に扱う部屋一覧です。通常は roomRoots から null を除いた結果が入ります。セットアップ実行時に更新されます。")]
    public GameObject[] detectedRoomRoots;

    [Tooltip("各部屋で表示切替対象として採用された Renderer 数です。セットアップ結果の確認用です。")]
    public int[] roomRendererCounts;

    [Tooltip("各部屋配下で見つかった Renderer 総数です。除外されたものや対象外のものも含む確認用カウントです。")]
    public int[] roomTotalRendererCounts;

    [Header("3. Last Build Result")]
    [TextArea(4, 12)]
    [Tooltip("最後にセットアップ実行・再構築・検証を行った結果です。警告やエラー、収集数の要約が入ります。設定変更後の確認に使います。")]
    public string lastBuildReport;

    [Tooltip("探索対象のルート配下で見つかった Renderer 数です。セットアップ時の収集規模の目安です。")]
    public int lastCollectedRendererCount;

    [Tooltip("実際に表示制御対象として採用された Renderer 数です。除外設定や activeToggleRoots の影響を反映した結果です。")]
    public int lastControlledRendererCount;

    [Tooltip("visibilityExcludeRoots によって常時表示扱いに分類された Renderer 数です。")]
    public int lastExcludedRendererCount;

    [FormerlySerializedAs("lastGeneratedVolumeCount")]
    [Tooltip("現在有効な部屋バウンディングボックスの数です。roomRoots 数と一致しない場合は、生成失敗や未設定の部屋がある可能性があります。")]
    public int lastGeneratedBoundingBoxCount;

    [Tooltip("Runtime が最後に判定した現在部屋のインデックスです。-1 はどの部屋にも入っていない室外状態を表します。")]
    public int currentRoomIndex = -1;

    private float _nextCheckTime;
    private float _nextDebugStateLogTime;
    private bool _startupWaitLogged;

    private void Reset()
    {
        if (boundingBoxParent == null)
        {
            boundingBoxParent = transform;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (boundingBoxParent == null)
        {
            boundingBoxParent = transform;
        }
    }
#endif

    private void Start()
    {
        currentRoomIndex = -1;
        _nextCheckTime = Time.time + Mathf.Max(0f, startupDelaySeconds);
        _nextDebugStateLogTime = _nextCheckTime;
        _startupWaitLogged = false;
        LogInfo(
            "Start",
            "startupDelay=" + startupDelaySeconds.ToString("0.###") +
            ", checkInterval=" + checkInterval.ToString("0.###") +
            ", boundsMargin=" + boundsMargin.ToString("0.###") +
            ", boundsHysteresisMargin=" + boundsHysteresisMargin.ToString("0.###") +
            ", roomCount=" + GetResolvedRoomCount() +
            ", boundingBoxes=" + SafeLength(roomBoundingBoxes) +
            ", matrixTargets=" + GetSerializedVisibilityMatrixTargetCount());
        LogRuntimeConfigurationWarnings();
        LogDebug(
            "Start",
            "startupDelay=" + startupDelaySeconds.ToString("0.###") +
            ", checkInterval=" + checkInterval.ToString("0.###") +
            ", boundsMargin=" + boundsMargin.ToString("0.###") +
            ", boundsHysteresisMargin=" + boundsHysteresisMargin.ToString("0.###") +
            ", roomCount=" + GetResolvedRoomCount() +
            ", boundingBoxes=" + SafeLength(roomBoundingBoxes) +
            ", matrixTargets=" + GetSerializedVisibilityMatrixTargetCount());
    }

    private void Update()
    {
        if (Time.time < _nextCheckTime)
        {
            LogDebugVisibilitySnapshotIfDue();
            return;
        }

        if (!_startupWaitLogged)
        {
            _startupWaitLogged = true;
            LogDebug("StartupDelayFinished", "time=" + Time.time.ToString("0.###"));
        }

        _nextCheckTime = Time.time + Mathf.Max(0.02f, checkInterval);
        EvaluateCurrentRoom();
        LogDebugVisibilitySnapshotIfDue();
    }

    public void ForceRefreshVisibility()
    {
        currentRoomIndex = DetermineCurrentRoomIndex();
        ApplyVisibility();
    }

    public string GetCurrentRoomName()
    {
        GameObject[] configuredRooms = GetConfiguredRooms();
        if (currentRoomIndex < 0 || configuredRooms == null || currentRoomIndex >= configuredRooms.Length)
        {
            return "(Outside)";
        }

        GameObject currentRoom = configuredRooms[currentRoomIndex];
        return currentRoom == null ? "(Missing Room)" : currentRoom.name;
    }

    public int GetConfiguredRoomCount()
    {
        GameObject[] configuredRooms = GetConfiguredRooms();
        return configuredRooms == null ? 0 : configuredRooms.Length;
    }

    public int GetVisibilityMatrixRowCount()
    {
        return GetVisibilityTargetCount();
    }

    public int GetValidBoundingBoxCount()
    {
        if (roomBoundingBoxes == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < roomBoundingBoxes.Length; i++)
        {
            if (roomBoundingBoxes[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void EvaluateCurrentRoom()
    {
        int roomIndex = DetermineCurrentRoomIndex();
        LogDebug(
            "EvaluateCurrentRoom",
            "detectedRoomIndex=" + roomIndex +
            ", previousRoomIndex=" + currentRoomIndex +
            ", roomName=" + GetRoomNameByIndex(roomIndex));

        if (roomIndex == currentRoomIndex)
        {
            LogDebug("EvaluateCurrentRoom", "Room unchanged; ApplyVisibility skipped");
            return;
        }

        currentRoomIndex = roomIndex;
        ApplyVisibility();
    }

    private int DetermineCurrentRoomIndex()
    {
        if (roomBoundingBoxes == null || roomBoundingBoxes.Length == 0)
        {
            LogWarning("DetermineCurrentRoomIndex", "No bounding boxes; treating player as outside");
            LogDebug("DetermineCurrentRoomIndex", "No bounding boxes");
            return -1;
        }

        int roomCount = GetResolvedRoomCount();
        int searchCount = roomCount > 0 ? Mathf.Min(roomBoundingBoxes.Length, roomCount) : roomBoundingBoxes.Length;
        Vector3 playerPosition = GetPlayerPosition();
        LogDebug(
            "DetermineCurrentRoomIndex",
            "playerPosition=" + Vector3ToLogString(playerPosition) +
            ", searchCount=" + searchCount +
            ", roomCount=" + roomCount +
            ", boundsMargin=" + boundsMargin.ToString("0.###") +
            ", boundsHysteresisMargin=" + boundsHysteresisMargin.ToString("0.###"));

        float safeBoundsMargin = Mathf.Max(0f, boundsMargin);
        float safeHysteresisMargin = Mathf.Max(0f, boundsHysteresisMargin);
        if (currentRoomIndex >= 0 && currentRoomIndex < searchCount)
        {
            BoxCollider currentBoundingBox = roomBoundingBoxes[currentRoomIndex];
            if (currentBoundingBox != null && IsInsideBoundingBox(currentBoundingBox, playerPosition, safeBoundsMargin + safeHysteresisMargin))
            {
                LogDebug("DetermineCurrentRoomIndex", "Stay in currentRoomIndex=" + currentRoomIndex + " with hysteresis");
                return currentRoomIndex;
            }
        }

        for (int i = 0; i < searchCount; i++)
        {
            if (i == currentRoomIndex)
            {
                continue;
            }

            BoxCollider boundingBox = roomBoundingBoxes[i];
            if (boundingBox == null)
            {
                LogDebug("DetermineCurrentRoomIndex", "boundingBox[" + i + "]=null");
                continue;
            }

            if (IsInsideBoundingBox(boundingBox, playerPosition, safeBoundsMargin))
            {
                LogDebug("DetermineCurrentRoomIndex", "Hit boundingBox[" + i + "]=" + boundingBox.name);
                return i;
            }
        }

        LogDebug("DetermineCurrentRoomIndex", "No hit");
        return -1;
    }

    private Vector3 GetPlayerPosition()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null)
        {
            return transform.position;
        }

        if (playerPoint == RoomVisibilityPlayerPoint.Head)
        {
            return localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        }

        return localPlayer.GetPosition();
    }

    private static bool IsInsideBoundingBox(BoxCollider boundingBox, Vector3 worldPosition, float margin)
    {
        Transform boundingBoxTransform = boundingBox.transform;
        Vector3 localPoint = boundingBoxTransform.InverseTransformPoint(worldPosition) - boundingBox.center;
        Vector3 halfSize = boundingBox.size * 0.5f;

        return Mathf.Abs(localPoint.x) <= halfSize.x + margin
            && Mathf.Abs(localPoint.y) <= halfSize.y + margin
            && Mathf.Abs(localPoint.z) <= halfSize.z + margin;
    }

    private void ApplyVisibility()
    {
        int currentStateIndex = GetCurrentStateIndex();
        bool showNonRoomObjects = IsVisibilityTargetVisible(currentStateIndex, 0);
        LogDebug(
            "ApplyVisibility",
            "currentRoomIndex=" + currentRoomIndex +
            ", currentStateIndex=" + currentStateIndex +
            ", currentRoomName=" + GetCurrentRoomName() +
            ", showNonRoomObjects=" + showNonRoomObjects +
            ", roomCount=" + GetResolvedRoomCount() +
            ", matrixTargets=" + GetSerializedVisibilityMatrixTargetCount() +
            ", controlledRenderers=" + SafeLength(controlledRenderers) +
            ", roomRenderers=" + SafeLength(roomRenderers) +
            ", controlledCanvases=" + SafeLength(controlledCanvases) +
            ", roomCanvases=" + SafeLength(roomCanvases) +
            ", controlledLights=" + SafeLength(controlledLights) +
            ", roomLights=" + SafeLength(roomLights) +
            ", controlledTerrains=" + SafeLength(controlledTerrainObjects) +
            ", controlledActiveObjects=" + SafeLength(controlledActiveObjects) +
            ", roomActiveObjects=" + SafeLength(roomActiveObjects));

        if (controlledRenderers != null)
        {
            for (int i = 0; i < controlledRenderers.Length; i++)
            {
                Renderer target = controlledRenderers[i];
                if (target == null)
                {
                    continue;
                }

                SetRendererVisibility(target, showNonRoomObjects);
            }
        }

        if (roomRenderers != null)
        {
            for (int i = 0; i < roomRenderers.Length; i++)
            {
                Renderer target = roomRenderers[i];
                if (target != null)
                {
                    int roomIndex = GetCachedRoomIndex(roomRendererRoomIndices, i);
                    SetRendererVisibility(target, IsVisibilityTargetVisible(currentStateIndex, roomIndex + 1));
                }
            }
        }

        if (excludedRenderers != null)
        {
            for (int i = 0; i < excludedRenderers.Length; i++)
            {
                Renderer target = excludedRenderers[i];
                if (target != null)
                {
                    SetRendererVisibility(target, true);
                }
            }
        }

        if (controlledCanvases != null)
        {
            for (int i = 0; i < controlledCanvases.Length; i++)
            {
                Canvas target = controlledCanvases[i];
                if (target != null)
                {
                    SetCanvasVisibility(target, showNonRoomObjects);
                }
            }
        }

        if (roomCanvases != null)
        {
            for (int i = 0; i < roomCanvases.Length; i++)
            {
                Canvas target = roomCanvases[i];
                if (target == null)
                {
                    continue;
                }

                int roomIndex = GetCachedRoomIndex(roomCanvasRoomIndices, i);
                SetCanvasVisibility(target, IsVisibilityTargetVisible(currentStateIndex, roomIndex + 1));
            }
        }

        if (excludedCanvases != null)
        {
            for (int i = 0; i < excludedCanvases.Length; i++)
            {
                Canvas target = excludedCanvases[i];
                if (target != null)
                {
                    SetCanvasVisibility(target, true);
                }
            }
        }

        if (controlledLights != null)
        {
            for (int i = 0; i < controlledLights.Length; i++)
            {
                Light target = controlledLights[i];
                if (target != null)
                {
                    SetLightVisibility(target, showNonRoomObjects);
                }
            }
        }

        if (roomLights != null)
        {
            for (int i = 0; i < roomLights.Length; i++)
            {
                Light target = roomLights[i];
                if (target == null)
                {
                    continue;
                }

                int roomIndex = GetCachedRoomIndex(roomLightRoomIndices, i);
                SetLightVisibility(target, IsVisibilityTargetVisible(currentStateIndex, roomIndex + 1));
            }
        }

        if (excludedLights != null)
        {
            for (int i = 0; i < excludedLights.Length; i++)
            {
                Light target = excludedLights[i];
                if (target != null)
                {
                    SetLightVisibility(target, true);
                }
            }
        }

        if (controlledTerrainObjects != null)
        {
            for (int i = 0; i < controlledTerrainObjects.Length; i++)
            {
                GameObject terrainObject = controlledTerrainObjects[i];
                if (terrainObject == null)
                {
                    continue;
                }

                terrainObject.SetActive(showNonRoomObjects);
            }
        }

        if (controlledActiveObjects != null)
        {
            for (int i = 0; i < controlledActiveObjects.Length; i++)
            {
                GameObject targetObject = controlledActiveObjects[i];
                if (targetObject != null)
                {
                    targetObject.SetActive(showNonRoomObjects);
                }
            }
        }

        if (roomActiveObjects != null)
        {
            for (int i = 0; i < roomActiveObjects.Length; i++)
            {
                GameObject targetObject = roomActiveObjects[i];
                if (targetObject == null)
                {
                    continue;
                }

                int roomIndex = GetCachedRoomIndex(roomActiveObjectRoomIndices, i);
                targetObject.SetActive(IsVisibilityTargetVisible(currentStateIndex, roomIndex + 1));
            }
        }
    }

    private int GetCurrentStateIndex()
    {
        int roomCount = GetResolvedRoomCount();
        if (currentRoomIndex < 0 || roomCount <= 0)
        {
            return 0;
        }

        int clampedRoomIndex = Mathf.Clamp(currentRoomIndex, 0, roomCount - 1);
        return clampedRoomIndex + 1;
    }

    private string GetRoomNameByIndex(int roomIndex)
    {
        GameObject[] configuredRooms = GetConfiguredRooms();
        if (roomIndex < 0 || configuredRooms == null || roomIndex >= configuredRooms.Length)
        {
            return "(Outside)";
        }

        GameObject room = configuredRooms[roomIndex];
        return room == null ? "(Missing Room)" : room.name;
    }

    private int GetCachedRoomIndex(int[] cachedRoomIndices, int itemIndex)
    {
        if (cachedRoomIndices == null || itemIndex < 0 || itemIndex >= cachedRoomIndices.Length)
        {
            return -1;
        }

        return cachedRoomIndices[itemIndex];
    }

    private GameObject[] GetConfiguredRooms()
    {
        if (detectedRoomRoots != null && detectedRoomRoots.Length > 0)
        {
            return detectedRoomRoots;
        }

        if (roomRoots == null || roomRoots.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < roomRoots.Length; i++)
        {
            if (roomRoots[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        GameObject[] configuredRooms = new GameObject[validCount];
        int index = 0;
        for (int i = 0; i < roomRoots.Length; i++)
        {
            if (roomRoots[i] != null)
            {
                configuredRooms[index] = roomRoots[i];
                index++;
            }
        }

        return configuredRooms;
    }

    private int GetResolvedRoomCount()
    {
        int configuredRoomCount = GetConfiguredRoomCount();
        if (configuredRoomCount > 0)
        {
            return configuredRoomCount;
        }

        int serializedTargetCount = GetSerializedVisibilityMatrixTargetCount();
        if (serializedTargetCount > 0)
        {
            return serializedTargetCount - 1;
        }

        return roomBoundingBoxes == null ? 0 : roomBoundingBoxes.Length;
    }

    private int GetVisibilityTargetCount()
    {
        int roomCount = GetResolvedRoomCount();
        if (roomCount > 0)
        {
            return roomCount + 1;
        }

        return 1;
    }

    private int GetSerializedVisibilityMatrixTargetCount()
    {
        if (roomHiddenMatrix == null)
        {
            return 0;
        }

        int length = roomHiddenMatrix.Length;
        if (length <= 0)
        {
            return 0;
        }

        int squareRoot = Mathf.RoundToInt(Mathf.Sqrt(length));
        if (squareRoot * squareRoot != length)
        {
            return 0;
        }

        return squareRoot;
    }

    private bool IsVisibilityTargetVisible(int stateIndex, int targetIndex)
    {
        if (targetIndex < 0)
        {
            return false;
        }

        int targetCount = GetVisibilityTargetCount();
        if (targetCount <= 0)
        {
            return false;
        }

        if (stateIndex < 0 || stateIndex >= targetCount || targetIndex >= targetCount)
        {
            return false;
        }

        if (stateIndex == targetIndex)
        {
            return true;
        }

        int serializedTargetCount = GetSerializedVisibilityMatrixTargetCount();
        if (serializedTargetCount <= 0)
        {
            return stateIndex == targetIndex;
        }

        if (stateIndex >= serializedTargetCount || targetIndex >= serializedTargetCount)
        {
            return stateIndex == targetIndex;
        }

        int matrixIndex = stateIndex * serializedTargetCount + targetIndex;
        return roomHiddenMatrix[matrixIndex];
    }

    private void LogDebugVisibilitySnapshotIfDue()
    {
        if (!enableDebugLogs || Time.time < _nextDebugStateLogTime)
        {
            return;
        }

        _nextDebugStateLogTime = Time.time + 1f;
        LogDebug("VisibilitySnapshot", BuildVisibilitySnapshotMessage());
    }

    private string BuildVisibilitySnapshotMessage()
    {
        int currentStateIndex = GetCurrentStateIndex();
        int targetCount = GetVisibilityTargetCount();
        string message =
            "currentLocation=" + GetRoomNameByIndex(currentRoomIndex) +
            ", stateIndex=" + currentStateIndex;

        for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
        {
            message += ", " + GetVisibilityTargetName(targetIndex) + "=" +
                (IsVisibilityTargetVisible(currentStateIndex, targetIndex) ? "show" : "hide");
        }

        return message;
    }

    private string GetVisibilityTargetName(int targetIndex)
    {
        if (targetIndex == 0)
        {
            return "室外";
        }

        return GetRoomNameByIndex(targetIndex - 1);
    }

    private void LogRuntimeConfigurationWarnings()
    {
        int roomCount = GetResolvedRoomCount();
        int boundingBoxCount = SafeLength(roomBoundingBoxes);
        int validBoundingBoxCount = GetValidBoundingBoxCount();
        int matrixTargetCount = GetSerializedVisibilityMatrixTargetCount();

        if (roomCount <= 0)
        {
            LogWarning("Start", "No rooms are configured");
        }

        if (boundingBoxCount == 0)
        {
            LogWarning("Start", "No room bounding boxes are assigned");
        }
        else if (validBoundingBoxCount != roomCount)
        {
            LogWarning(
                "Start",
                "Room count and valid bounding box count differ: roomCount=" + roomCount +
                ", validBoundingBoxes=" + validBoundingBoxCount);
        }

        if (matrixTargetCount > 0 && matrixTargetCount != roomCount + 1)
        {
            LogWarning(
                "Start",
                "Visibility matrix size does not match room count: matrixTargets=" + matrixTargetCount +
                ", expectedTargets=" + (roomCount + 1));
        }
    }

    private void LogInfo(string eventName, string message)
    {
        Debug.Log("[RoomVisibilityManager] " + eventName + " | " + message, this);
    }

    private void LogWarning(string eventName, string message)
    {
        Debug.LogWarning("[RoomVisibilityManager] " + eventName + " | " + message, this);
    }

    private void LogDebug(string eventName, string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log("[RoomVisibilityManager] " + eventName + " | " + message, this);
    }

    private static int SafeLength(System.Array array)
    {
        return array == null ? 0 : array.Length;
    }

    private static string Vector3ToLogString(Vector3 value)
    {
        return "(" +
            value.x.ToString("0.###") + ", " +
            value.y.ToString("0.###") + ", " +
            value.z.ToString("0.###") + ")";
    }

    private void SetRendererVisibility(Renderer target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        target.enabled = visible;
    }

    private void SetCanvasVisibility(Canvas target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        target.enabled = visible;
    }

    private void SetLightVisibility(Light target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        target.enabled = visible;
    }
}
