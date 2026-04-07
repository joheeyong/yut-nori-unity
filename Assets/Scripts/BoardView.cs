using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates all Unity UI objects procedurally and keeps visuals in sync with
/// the GameController state.
/// Attach this MonoBehaviour to a GameObject in the scene (e.g. "GameManager").
/// It will create a GameController on the same GameObject automatically.
/// </summary>
public class BoardView : MonoBehaviour
{
    // -----------------------------------------------------------------------
    //  Private references
    // -----------------------------------------------------------------------
    GameController  _ctrl;
    Canvas          _canvas;
    RectTransform   _boardPanel;

    // Node visual circles
    Image[]         _nodeImages;           // one per board node (29)

    // Piece tokens: [player][piece]
    GameObject[][]  _pieceObjects;
    Image[][]       _pieceImages;
    TextMeshProUGUI[][] _pieceLabels;

    // UI text elements
    TextMeshProUGUI _turnLabel;
    TextMeshProUGUI _throwResultLabel;
    TextMeshProUGUI _statusLabel;
    TextMeshProUGUI _p0ScoreLabel;
    TextMeshProUGUI _p1ScoreLabel;
    Button          _throwButton;
    TextMeshProUGUI _throwButtonLabel;

    // Shared sprites
    Sprite _circleSprite;
    Sprite _whiteSquareSprite;

    // Board panel offset from canvas centre (we centre the board)
    const float BOARD_SIZE   = 560f;
    const float CANVAS_W     = 800f;
    const float CANVAS_H     = 600f;

    // Node radii
    const float NODE_RADIUS_NORMAL = 14f;
    const float NODE_RADIUS_CORNER = 18f;
    const float NODE_RADIUS_CENTER = 20f;
    const float NODE_RADIUS_START  = 20f;

    // Colours
    static readonly Color COL_BOARD_BG   = new Color(0.10f, 0.10f, 0.18f, 1f);
    static readonly Color COL_NODE_OUTER = new Color(0.85f, 0.75f, 0.50f, 1f);
    static readonly Color COL_NODE_INNER = new Color(0.95f, 0.90f, 0.70f, 1f);
    static readonly Color COL_LINE       = new Color(0.70f, 0.60f, 0.35f, 0.85f);
    static readonly Color COL_HIGHLIGHT  = new Color(1.00f, 0.95f, 0.20f, 1f);
    static readonly Color COL_PANEL_BG   = new Color(0.08f, 0.08f, 0.16f, 0.95f);

    // Set of highlighted piece IDs per player
    HashSet<int> _highlightedPieces0 = new HashSet<int>();
    HashSet<int> _highlightedPieces1 = new HashSet<int>();

    string _lastMessage = "";

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------
    void Start()
    {
        // Create shared sprites
        _circleSprite      = CreateCircleSprite(64);
        _whiteSquareSprite = CreateSquareSprite();

        // Create GameController on the same GO
        _ctrl            = gameObject.GetComponent<GameController>();
        if (_ctrl == null) _ctrl = gameObject.AddComponent<GameController>();
        _ctrl.boardView  = this;

        // Build UI
        BuildCanvas();
        BuildTopPanel();
        BuildBoardPanel();
        BuildBottomPanel();

        RefreshAll();
    }

    // -----------------------------------------------------------------------
    //  Sprite helpers
    // -----------------------------------------------------------------------
    static Sprite CreateCircleSprite(int size = 64)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float ctr  = size / 2f;
        float r    = ctr - 1f;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dist  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(ctr, ctr));
            float alpha = Mathf.Clamp01((r - dist) / 1.5f);
            tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }

    static Sprite CreateSquareSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
    }

    // -----------------------------------------------------------------------
    //  Canvas
    // -----------------------------------------------------------------------
    void BuildCanvas()
    {
        var canvasGO = new GameObject("YutnoriCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CANVAS_W, CANVAS_H);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
    }

    // -----------------------------------------------------------------------
    //  Top panel: turn label + throw result
    // -----------------------------------------------------------------------
    void BuildTopPanel()
    {
        var panel = CreatePanel("TopPanel", _canvas.transform,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0f, -30f), new Vector2(CANVAS_W, 56f));
        SetImage(panel, COL_PANEL_BG);

        _turnLabel = CreateLabel("TurnLabel", panel.transform,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0, 8f),
            new Vector2(400f, 36f), "플레이어 1의 차례", 20);

        _throwResultLabel = CreateLabel("ThrowResultLabel", panel.transform,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0, -16f),
            new Vector2(400f, 26f), "", 16);
        _throwResultLabel.color = new Color(1f, 0.9f, 0.3f);
    }

    // -----------------------------------------------------------------------
    //  Board panel
    // -----------------------------------------------------------------------
    void BuildBoardPanel()
    {
        // Main board container
        var panelGO = new GameObject("BoardPanel");
        panelGO.transform.SetParent(_canvas.transform, false);
        var rt = panelGO.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(BOARD_SIZE, BOARD_SIZE);
        rt.anchoredPosition = Vector2.zero;
        SetImage(panelGO, COL_BOARD_BG);
        _boardPanel = rt;

        // Draw board lines first (behind nodes)
        foreach (var (a, b) in BoardData.BoardLines)
            DrawLine(a, b);

        // Draw nodes
        _nodeImages = new Image[BoardData.NodePositions.Length];
        for (int i = 0; i < BoardData.NodePositions.Length; i++)
            _nodeImages[i] = CreateNodeImage(i);

        // Create piece tokens
        _pieceObjects = new GameObject[2][];
        _pieceImages  = new Image[2][];
        _pieceLabels  = new TextMeshProUGUI[2][];

        for (int p = 0; p < 2; p++)
        {
            _pieceObjects[p] = new GameObject[4];
            _pieceImages[p]  = new Image[4];
            _pieceLabels[p]  = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Piece_P{p}_{i}");
                go.transform.SetParent(_boardPanel, false);

                var img = go.AddComponent<Image>();
                img.sprite = _circleSprite;
                img.color  = BoardData.PlayerColors[p];
                img.raycastTarget = true;

                var imgRt = go.GetComponent<RectTransform>();
                imgRt.sizeDelta = new Vector2(28f, 28f);

                // Add click handler
                var btn = go.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int captureP = p, captureI = i;
                btn.onClick.AddListener(() => _ctrl.OnPieceClicked(captureP, captureI));

                // Label (stack count)
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(go.transform, false);
                var lbl = labelGO.AddComponent<TextMeshProUGUI>();
                lbl.text      = "";
                lbl.fontSize  = 11f;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color     = Color.white;
                var lblRt = labelGO.GetComponent<RectTransform>();
                lblRt.anchorMin = lblRt.anchorMax = new Vector2(0.5f, 0.5f);
                lblRt.pivot     = new Vector2(0.5f, 0.5f);
                lblRt.sizeDelta = new Vector2(28f, 28f);
                lblRt.anchoredPosition = Vector2.zero;

                _pieceObjects[p][i] = go;
                _pieceImages[p][i]  = img;
                _pieceLabels[p][i]  = lbl;
            }
        }

        // Home area labels (left for P0, right for P1)
        CreateHomeAreaLabels();
    }

    Image CreateNodeImage(int nodeIndex)
    {
        bool isCorner = (nodeIndex == 5 || nodeIndex == 10 || nodeIndex == 15);
        bool isStart  = (nodeIndex == 0);
        bool isCenter = (nodeIndex == 22);

        float radius = isCenter ? NODE_RADIUS_CENTER
                     : (isCorner || isStart) ? NODE_RADIUS_CORNER
                     : NODE_RADIUS_NORMAL;

        var go = new GameObject($"Node_{nodeIndex}");
        go.transform.SetParent(_boardPanel, false);

        var img = go.AddComponent<Image>();
        img.sprite = _circleSprite;
        img.raycastTarget = false;

        if (isStart)
            img.color = new Color(0.2f, 0.8f, 0.3f, 1f); // green = start/finish
        else if (isCorner)
            img.color = new Color(0.9f, 0.5f, 0.1f, 1f); // orange = corner shortcuts
        else if (isCenter)
            img.color = new Color(0.8f, 0.3f, 0.8f, 1f); // purple = center
        else
            img.color = COL_NODE_OUTER;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(radius * 2f, radius * 2f);
        rt.anchoredPosition = BoardData.NodePositions[nodeIndex];

        // For start node, add "출발" label
        if (isStart)
        {
            var lgo = new GameObject("StartLabel");
            lgo.transform.SetParent(go.transform, false);
            var lbl = lgo.AddComponent<TextMeshProUGUI>();
            lbl.text      = "출";
            lbl.fontSize  = 9f;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color     = Color.white;
            var lrt = lgo.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta        = new Vector2(30f, 20f);
            lrt.anchoredPosition = Vector2.zero;
        }

        return img;
    }

    void DrawLine(int nodeA, int nodeB)
    {
        var posA = BoardData.NodePositions[nodeA];
        var posB = BoardData.NodePositions[nodeB];

        var go = new GameObject($"Line_{nodeA}_{nodeB}");
        go.transform.SetParent(_boardPanel, false);

        var img = go.AddComponent<Image>();
        img.sprite = _whiteSquareSprite;
        img.color  = COL_LINE;
        img.raycastTarget = false;

        float dist  = Vector2.Distance(posA, posB);
        float angle = Mathf.Atan2(posB.y - posA.y, posB.x - posA.x) * Mathf.Rad2Deg;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(dist, 3f);
        rt.anchoredPosition = (posA + posB) * 0.5f;
        rt.localRotation    = Quaternion.Euler(0, 0, angle);
    }

    void CreateHomeAreaLabels()
    {
        // Player 0 home area — bottom-left of canvas
        CreateLabel("P0HomeLabel", _boardPanel,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(-240f, -290f),
            new Vector2(120f, 26f), "P1 대기", 12).color = BoardData.PlayerColors[0];

        // Player 1 home area — bottom-right of canvas
        CreateLabel("P1HomeLabel", _boardPanel,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(240f, -290f),
            new Vector2(120f, 26f), "AI 대기", 12).color = BoardData.PlayerColors[1];
    }

    // -----------------------------------------------------------------------
    //  Bottom panel: throw button + status
    // -----------------------------------------------------------------------
    void BuildBottomPanel()
    {
        var panel = CreatePanel("BottomPanel", _canvas.transform,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(0f, 30f), new Vector2(CANVAS_W, 56f));
        SetImage(panel, COL_PANEL_BG);

        // Score labels
        _p0ScoreLabel = CreateLabel("P0Score", panel.transform,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(-300f, 10f),
            new Vector2(140f, 50f), "P1: 0/4 완료", 13);
        _p0ScoreLabel.color = BoardData.PlayerColors[0];

        _p1ScoreLabel = CreateLabel("P1Score", panel.transform,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(300f, 10f),
            new Vector2(140f, 50f), "AI: 0/4 완료", 13);
        _p1ScoreLabel.color = BoardData.PlayerColors[1];

        // Throw button
        var btnGO = new GameObject("ThrowButton");
        btnGO.transform.SetParent(panel.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.sprite = _whiteSquareSprite;
        btnImg.color  = new Color(0.15f, 0.55f, 0.85f, 1f);
        var btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta        = new Vector2(120f, 44f);
        btnRt.anchoredPosition = new Vector2(-80f, 4f);

        _throwButton = btnGO.AddComponent<Button>();
        _throwButton.onClick.AddListener(_ctrl.OnThrowClicked);

        var btnColors = _throwButton.colors;
        btnColors.normalColor      = new Color(0.15f, 0.55f, 0.85f, 1f);
        btnColors.highlightedColor = new Color(0.25f, 0.70f, 1.00f, 1f);
        btnColors.pressedColor     = new Color(0.10f, 0.40f, 0.65f, 1f);
        btnColors.disabledColor    = new Color(0.30f, 0.30f, 0.30f, 0.6f);
        _throwButton.colors        = btnColors;

        _throwButtonLabel = CreateLabel("ThrowBtnLabel", btnGO.transform,
            Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(120f, 44f), "던지기", 18);

        // Status label
        _statusLabel = CreateLabel("StatusLabel", panel.transform,
            Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(80f, 4f),
            new Vector2(220f, 50f), "", 14);
        _statusLabel.color = new Color(1f, 0.9f, 0.5f);
    }

    // -----------------------------------------------------------------------
    //  Public update methods (called by GameController)
    // -----------------------------------------------------------------------

    public void RefreshAll()
    {
        ClearHighlights();
        PositionAllPieces();
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (_ctrl == null) return;

        // Turn label
        if (_ctrl.State == GameState.GAME_OVER)
        {
            _turnLabel.text = _lastMessage;
        }
        else
        {
            _turnLabel.text = _ctrl.CurrentPlayer == 0 ? "플레이어 1의 차례" : "AI의 차례";
        }

        // Throw button
        bool canThrow = _ctrl.State == GameState.WAITING_THROW && _ctrl.CurrentPlayer == 0;
        _throwButton.interactable = canThrow;
        _throwButtonLabel.color   = canThrow ? Color.white : new Color(0.6f, 0.6f, 0.6f);

        // Pending throws display
        if (_ctrl.PendingThrows.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("남은 이동: ");
            foreach (var t in _ctrl.PendingThrows)
                sb.Append(BoardData.StepsToName(t) + " ");
            _statusLabel.text = sb.ToString();
        }
        else if (_ctrl.State == GameState.WAITING_PIECE_SELECT)
        {
            _statusLabel.text = "말을 선택하세요";
        }
        else if (_ctrl.State == GameState.AI_TURN)
        {
            _statusLabel.text = "AI 생각 중...";
        }
        else
        {
            _statusLabel.text = _lastMessage;
        }

        // Score labels
        _p0ScoreLabel.text = $"P1: {_ctrl.GetFinishedCount(0)}/4 완료";
        _p1ScoreLabel.text = $"AI: {_ctrl.GetFinishedCount(1)}/4 완료";
    }

    public void ShowThrowResult(int steps, bool isBonus)
    {
        string name   = BoardData.StepsToName(steps);
        string extra  = isBonus ? " (한 번 더!)" : "";
        _throwResultLabel.text = $"{name} ({steps}칸){extra}";
    }

    public void ShowMessage(string msg)
    {
        _lastMessage      = msg;
        _statusLabel.text = msg;
    }

    public void HighlightMovablePieces(int player, int steps)
    {
        ClearHighlights();
        var movable = GameLogic.GetMovablePieces(player, _ctrl.Pieces);
        foreach (var piece in movable)
        {
            if (player == 0) _highlightedPieces0.Add(piece.pieceId);
            else             _highlightedPieces1.Add(piece.pieceId);
        }
        ApplyHighlights();
    }

    public void HighlightSinglePiece(int player, int pieceId)
    {
        ClearHighlights();
        if (player == 0) _highlightedPieces0.Add(pieceId);
        else             _highlightedPieces1.Add(pieceId);
        ApplyHighlights();
    }

    void ClearHighlights()
    {
        _highlightedPieces0.Clear();
        _highlightedPieces1.Clear();
        ApplyHighlights();
    }

    void ApplyHighlights()
    {
        if (_pieceImages == null) return;
        for (int p = 0; p < 2; p++)
        {
            var set = p == 0 ? _highlightedPieces0 : _highlightedPieces1;
            for (int i = 0; i < 4; i++)
            {
                if (_pieceImages[p] == null || _pieceImages[p][i] == null) continue;
                // Use a bright outline effect by scaling slightly and tinting
                // We'll just tint the image brighter
                if (set.Contains(i))
                    _pieceImages[p][i].color = Color.Lerp(BoardData.PlayerColors[p], COL_HIGHLIGHT, 0.5f);
                else
                    _pieceImages[p][i].color = BoardData.PlayerColors[p];
            }
        }
    }

    // -----------------------------------------------------------------------
    //  Piece positioning
    // -----------------------------------------------------------------------

    void PositionAllPieces()
    {
        if (_ctrl == null || _pieceObjects == null) return;

        // Track which nodes have been used so we can offset stacked pieces
        // node -> list of (player, pieceId) in render order
        var nodeOccupants = new Dictionary<int, List<(int p, int i)>>();

        // First pass: collect occupancy
        for (int p = 0; p < 2; p++)
        for (int i = 0; i < 4; i++)
        {
            var piece = _ctrl.Pieces[p][i];
            int node  = piece.CurrentNode(BoardData.Routes);
            if (node < 0) continue; // home or finished

            if (!nodeOccupants.ContainsKey(node))
                nodeOccupants[node] = new List<(int, int)>();
            nodeOccupants[node].Add((p, i));
        }

        // Home slot counters
        int[] homeSlot = new int[2];

        // Second pass: position each piece
        for (int p = 0; p < 2; p++)
        for (int i = 0; i < 4; i++)
        {
            var piece = _ctrl.Pieces[p][i];
            var go    = _pieceObjects[p][i];
            var img   = _pieceImages[p][i];
            var lbl   = _pieceLabels[p][i];

            if (piece.IsFinished(BoardData.Routes))
            {
                // Show in a "finished" area at bottom of screen, below board
                var rt = go.GetComponent<RectTransform>();
                float fx = (p == 0 ? -200f : 200f) + (homeSlot[p] % 4) * 20f;
                rt.anchoredPosition = new Vector2(fx, -310f);
                rt.sizeDelta        = new Vector2(22f, 22f);
                img.color           = new Color(BoardData.PlayerColors[p].r,
                                                BoardData.PlayerColors[p].g,
                                                BoardData.PlayerColors[p].b, 0.5f);
                lbl.text = "✓";
                homeSlot[p]++;
                go.SetActive(true);
                continue;
            }

            if (piece.IsHome)
            {
                // Stack home pieces in a column beside the board
                float hx = p == 0 ? -310f : 310f;
                float hy = 60f - homeSlot[p] * 36f;
                var rt = go.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(hx, hy);
                rt.sizeDelta        = new Vector2(28f, 28f);
                img.color           = BoardData.PlayerColors[p];
                lbl.text            = "";
                homeSlot[p]++;
                go.SetActive(true);
                continue;
            }

            // On-board piece
            int node = piece.CurrentNode(BoardData.Routes);
            Vector2 basePos = BoardData.NodePositions[node];

            // Find this piece's index within the node's occupant list
            var occupants = nodeOccupants.ContainsKey(node) ? nodeOccupants[node] : null;
            int slotIdx   = 0;
            int totalInNode = 1;
            if (occupants != null)
            {
                totalInNode = occupants.Count;
                for (int k = 0; k < occupants.Count; k++)
                    if (occupants[k].p == p && occupants[k].i == i) { slotIdx = k; break; }
            }

            // Offset pieces slightly if stacked
            Vector2 offset = Vector2.zero;
            if (totalInNode > 1)
            {
                float spread = 10f;
                offset.x = (slotIdx - (totalInNode - 1) * 0.5f) * spread;
                offset.y = (slotIdx % 2 == 0) ? spread * 0.5f : -spread * 0.5f;
            }

            var pieceRt = go.GetComponent<RectTransform>();
            pieceRt.anchoredPosition = basePos + offset;
            pieceRt.sizeDelta        = new Vector2(28f, 28f);

            img.color = BoardData.PlayerColors[p];

            // Count how many of this player's pieces are at this node (for stack label)
            int friendCount = 0;
            foreach (var occ in nodeOccupants[node])
                if (occ.p == p) friendCount++;

            // Only show label on the first piece in the stack (slot 0 for this player)
            bool isFirstOfPlayer = true;
            foreach (var occ in nodeOccupants[node])
            {
                if (occ.p == p && occ.i < i) { isFirstOfPlayer = false; break; }
            }

            lbl.text = (friendCount > 1 && isFirstOfPlayer) ? friendCount.ToString() : "";
            go.SetActive(true);
        }
    }

    // -----------------------------------------------------------------------
    //  UI factory helpers
    // -----------------------------------------------------------------------

    static GameObject CreatePanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;
        return go;
    }

    static void SetImage(GameObject go, Color color)
    {
        var img    = go.AddComponent<Image>();
        var tex    = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        img.color  = color;
        img.raycastTarget = false;
    }

    static TextMeshProUGUI CreateLabel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
        Vector2 size, string text, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text      = text;
        lbl.fontSize  = fontSize;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color     = Color.white;
        return lbl;
    }
}
