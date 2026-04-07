using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full visual layer for the Netmarble-style Yutnori game.
/// Creates every UI element procedurally; no prefabs or scene assets required.
/// Attach this MonoBehaviour to "GameManager" (done automatically by GameInit).
/// </summary>
public class BoardView : MonoBehaviour
{
    // -----------------------------------------------------------------------
    //  Constants
    // -----------------------------------------------------------------------
    const float CANVAS_W   = 900f;
    const float CANVAS_H   = 700f;
    const float BOARD_SIZE = 560f;

    // Shared node radius values
    const float R_NORMAL = 13f;
    const float R_CORNER = 20f;
    const float R_CENTER = 22f;
    const float R_START  = 22f;

    // Piece token sizes
    const float PIECE_SIZE      = 32f;
    const float PIECE_SIZE_HOME = 20f;

    // -----------------------------------------------------------------------
    //  Colour palette (Netmarble-inspired dark-navy / warm-wood)
    // -----------------------------------------------------------------------
    static readonly Color C_BG          = new Color(0.08f, 0.06f, 0.12f, 1f);   // very dark navy
    static readonly Color C_BOARD_BG    = new Color(0.45f, 0.28f, 0.08f, 1f);   // dark wood
    static readonly Color C_BOARD_INNER = new Color(0.52f, 0.33f, 0.10f, 1f);   // slightly lighter wood
    static readonly Color C_GOLD        = new Color(0.85f, 0.65f, 0.15f, 1f);   // gold border / lines
    static readonly Color C_GOLD_BRIGHT = new Color(0.95f, 0.80f, 0.25f, 1f);   // bright gold
    static readonly Color C_LINE_OUTER  = new Color(0.90f, 0.75f, 0.30f, 1f);   // outer ring lines
    static readonly Color C_LINE_DIAG   = new Color(0.85f, 0.65f, 0.20f, 0.9f); // diagonal shortcut lines
    static readonly Color C_NODE_NORMAL = new Color(0.60f, 0.40f, 0.15f, 1f);   // normal node fill
    static readonly Color C_NODE_CORNER = new Color(0.70f, 0.50f, 0.15f, 1f);   // corner node fill
    static readonly Color C_NODE_CENTER = new Color(0.75f, 0.55f, 0.10f, 1f);   // center node fill
    static readonly Color C_NODE_START  = new Color(0.80f, 0.20f, 0.10f, 1f);   // start node fill
    static readonly Color C_P0_PANEL    = new Color(0.70f, 0.15f, 0.15f, 0.92f);
    static readonly Color C_P1_PANEL    = new Color(0.15f, 0.20f, 0.70f, 0.92f);
    static readonly Color C_P0_PIECE    = new Color(0.95f, 0.30f, 0.30f, 1f);
    static readonly Color C_P1_PIECE    = new Color(0.30f, 0.50f, 0.95f, 1f);
    static readonly Color C_THROW_BTN   = new Color(0.85f, 0.60f, 0.10f, 1f);
    static readonly Color C_THROW_DIM   = new Color(0.45f, 0.35f, 0.10f, 1f);
    static readonly Color C_MSG_CAPTURE = new Color(1.0f,  0.35f, 0.15f, 1f);
    static readonly Color C_MSG_STACK   = new Color(0.30f, 1.0f,  0.50f, 1f);
    static readonly Color C_MSG_WIN     = new Color(1.0f,  0.90f, 0.10f, 1f);
    static readonly Color C_MSG_ARRIVE  = new Color(0.60f, 1.0f,  0.30f, 1f);
    static readonly Color C_HIGHLIGHT   = new Color(1.0f,  0.95f, 0.20f, 1f);

    static Color PieceColor(int p) => p == 0 ? C_P0_PIECE : C_P1_PIECE;
    static Color PanelColor(int p)  => p == 0 ? C_P0_PANEL : C_P1_PANEL;

    // -----------------------------------------------------------------------
    //  Private references
    // -----------------------------------------------------------------------
    GameController _ctrl;
    Canvas         _canvas;
    RectTransform  _boardContainer;   // 560×560 board panel
    RectTransform  _uiRoot;           // full-screen overlay for popups, etc.

    // Node visuals
    Image[]        _nodeImages;       // 29 node circles

    // Piece tokens – on-board layer
    GameObject[][] _pieceGOs;         // [2][4]
    Image[][]      _pieceImgs;
    RectTransform[][] _pieceRTs;
    GameObject[][]  _pieceShadowGOs;
    Image[][]       _pieceGlowImgs;   // pulsing glow ring behind piece
    TextMeshProUGUI[][] _pieceCountLabels; // stack count badge text

    // Piece tokens – home area (shown in player panels when piece is off board)
    Image[][]      _homeDotImgs;      // [2][4]

    // Player info panels
    RectTransform[] _playerPanels;       // [2]
    Image[]         _playerPanelBorders; // [2] – pulsing gold border
    TextMeshProUGUI[] _playerNameLabels; // [2]
    TextMeshProUGUI[] _playerScoreLabels;// [2]
    TextMeshProUGUI[] _playerTurnLabels; // [2] – "당신의 차례" / "AI 생각중..."

    // Bottom bar
    Button          _throwBtn;
    Image           _throwBtnImg;
    TextMeshProUGUI _throwBtnLabel;
    TextMeshProUGUI _throwResultLabel;
    TextMeshProUGUI _pendingThrowsLabel;

    // Yut stick visuals
    Image[]         _stickImgs;       // 4 sticks

    // Popup / message layer
    GameObject      _msgPopupGO;
    TextMeshProUGUI _msgPopupText;

    // Win overlay
    GameObject      _winOverlayGO;
    TextMeshProUGUI _winOverlayText;

    // Shared sprites (created once, reused)
    Sprite _circleSprite;
    Sprite _squareSprite;
    Sprite _roundedSprite;

    // Animation coroutine handles
    Coroutine _throwBtnPulseCoroutine;
    Coroutine _panelBorderPulseCoroutine;
    Coroutine[] _glowCoroutines; // [2*4]

    // Track highlighted pieces for glow management
    readonly HashSet<int> _glowP0 = new HashSet<int>();
    readonly HashSet<int> _glowP1 = new HashSet<int>();

    // Diagonal-line node index sets (for line colour lookup)
    static readonly HashSet<(int, int)> DiagLines = new HashSet<(int, int)>
    {
        (5, 20), (20, 21), (21, 22),
        (10, 25), (25, 26), (26, 22),
        (15, 27), (27, 28), (28, 22),
        (22, 23), (23, 24), (24, 0),
    };

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------

    void Start()
    {
        // Shared sprites
        _circleSprite  = UIHelper.CreateCircleSprite(64);
        _squareSprite  = MakeWhiteSquare();
        _roundedSprite = UIHelper.CreateRoundedRect(160, 60, 14);

        // Wire up controller
        _ctrl = gameObject.GetComponent<GameController>();
        if (_ctrl == null) _ctrl = gameObject.AddComponent<GameController>();
        _ctrl.boardView = this;

        // Subscribe to events
        _ctrl.OnThrowResult   += HandleThrowResult;
        _ctrl.OnPieceMove     += HandlePieceMove;
        _ctrl.OnCapture       += HandleCapture;
        _ctrl.OnStack         += HandleStack;
        _ctrl.OnPlayerWin     += HandleWin;
        _ctrl.OnTurnChange    += HandleTurnChange;
        _ctrl.OnBonusThrow    += HandleBonusThrow;
        _ctrl.OnPieceFinished += HandlePieceFinished;

        _glowCoroutines = new Coroutine[8];

        BuildCanvas();
        BuildBackground();
        BuildPlayerPanels();
        BuildBoardPanel();
        BuildBottomBar();
        BuildMessagePopup();
        BuildWinOverlay();

        RefreshAll();
    }

    void OnDestroy()
    {
        if (_ctrl == null) return;
        _ctrl.OnThrowResult   -= HandleThrowResult;
        _ctrl.OnPieceMove     -= HandlePieceMove;
        _ctrl.OnCapture       -= HandleCapture;
        _ctrl.OnStack         -= HandleStack;
        _ctrl.OnPlayerWin     -= HandleWin;
        _ctrl.OnTurnChange    -= HandleTurnChange;
        _ctrl.OnBonusThrow    -= HandleBonusThrow;
        _ctrl.OnPieceFinished -= HandlePieceFinished;
    }

    // -----------------------------------------------------------------------
    //  Build – Canvas
    // -----------------------------------------------------------------------

    void BuildCanvas()
    {
        var canvasGO = new GameObject("YutnoriCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CANVAS_W, CANVAS_H);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen UI root for overlays
        _uiRoot = UIHelper.CreateRect(_canvas.transform, "UIRoot",
                                      new Vector2(CANVAS_W, CANVAS_H), Vector2.zero);
    }

    // -----------------------------------------------------------------------
    //  Build – Background
    // -----------------------------------------------------------------------

    void BuildBackground()
    {
        var bg = UIHelper.CreateImage(_canvas.transform, "Background",
                                      new Vector2(CANVAS_W, CANVAS_H), Vector2.zero, C_BG);
        bg.raycastTarget = false;
        bg.rectTransform.SetAsFirstSibling();
    }

    // -----------------------------------------------------------------------
    //  Build – Player panels (top-left and top-right)
    // -----------------------------------------------------------------------

    void BuildPlayerPanels()
    {
        _playerPanels        = new RectTransform[2];
        _playerPanelBorders  = new Image[2];
        _playerNameLabels    = new TextMeshProUGUI[2];
        _playerScoreLabels   = new TextMeshProUGUI[2];
        _playerTurnLabels    = new TextMeshProUGUI[2];
        _homeDotImgs         = new Image[2][];

        // Panel positions: left (P0) and right (P1), near top
        Vector2[] panelPositions = { new Vector2(-320f, 310f), new Vector2(320f, 310f) };
        string[] names  = { "사람", "컴퓨터" };

        for (int p = 0; p < 2; p++)
        {
            var panelRT = UIHelper.CreateRect(_canvas.transform, $"PlayerPanel_{p}",
                                              new Vector2(220f, 95f), panelPositions[p]);
            _playerPanels[p] = panelRT;

            // Background
            var bgImg = panelRT.gameObject.AddComponent<Image>();
            bgImg.sprite = UIHelper.CreateRoundedRect(220, 95, 12);
            bgImg.type   = Image.Type.Sliced;
            bgImg.color  = PanelColor(p);
            bgImg.raycastTarget = false;

            // Gold border ring (separate image slightly larger, rendered behind bg)
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(panelRT, false);
            var borderRT = borderGO.AddComponent<RectTransform>();
            borderRT.anchorMin = borderRT.anchorMax = borderRT.pivot = Vector2.one * 0.5f;
            borderRT.sizeDelta = new Vector2(226f, 101f);
            borderRT.anchoredPosition = Vector2.zero;
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.sprite = UIHelper.CreateRoundedRect(226, 101, 14);
            borderImg.type   = Image.Type.Sliced;
            borderImg.color  = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0.0f);
            borderImg.raycastTarget = false;
            borderGO.transform.SetAsFirstSibling();
            _playerPanelBorders[p] = borderImg;

            // Name label
            _playerNameLabels[p] = UIHelper.CreateText(panelRT, "NameLabel",
                names[p], 18, Color.white,
                new Vector2(200f, 26f), new Vector2(0f, 30f));
            _playerNameLabels[p].fontStyle = FontStyles.Bold;

            // Turn indicator label
            _playerTurnLabels[p] = UIHelper.CreateText(panelRT, "TurnLabel",
                "", 13, C_GOLD_BRIGHT,
                new Vector2(200f, 20f), new Vector2(0f, 12f));

            // Score label
            _playerScoreLabels[p] = UIHelper.CreateText(panelRT, "ScoreLabel",
                "완료: 0/4", 13, Color.white,
                new Vector2(200f, 20f), new Vector2(0f, -6f));

            // Home dots row (4 dots showing piece status)
            _homeDotImgs[p] = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float dotX = (p == 0) ? (-60f + i * 20f) : (-60f + i * 20f);
                var dotImg = UIHelper.CreateImage(panelRT, $"HomeDot_{i}",
                    new Vector2(14f, 14f), new Vector2(dotX, -28f), PieceColor(p));
                dotImg.sprite = _circleSprite;
                _homeDotImgs[p][i] = dotImg;
            }
        }
    }

    // -----------------------------------------------------------------------
    //  Build – Board panel
    // -----------------------------------------------------------------------

    void BuildBoardPanel()
    {
        // Outer board panel (wood background)
        var boardPanelRT = UIHelper.CreateRect(_canvas.transform, "BoardPanel",
                                               new Vector2(BOARD_SIZE + 12f, BOARD_SIZE + 12f),
                                               new Vector2(0f, -20f));
        var boardBorderImg = boardPanelRT.gameObject.AddComponent<Image>();
        boardBorderImg.sprite = UIHelper.CreateRoundedRect(
            Mathf.RoundToInt(BOARD_SIZE + 12f), Mathf.RoundToInt(BOARD_SIZE + 12f), 10);
        boardBorderImg.type   = Image.Type.Sliced;
        boardBorderImg.color  = C_GOLD;
        boardBorderImg.raycastTarget = false;

        // Inner wood board container
        _boardContainer = UIHelper.CreateRect(boardPanelRT, "BoardContainer",
                                              new Vector2(BOARD_SIZE, BOARD_SIZE), Vector2.zero);
        var boardImg = _boardContainer.gameObject.AddComponent<Image>();
        boardImg.sprite = UIHelper.CreateRoundedRect(Mathf.RoundToInt(BOARD_SIZE),
                                                      Mathf.RoundToInt(BOARD_SIZE), 8);
        boardImg.type   = Image.Type.Sliced;
        boardImg.color  = C_BOARD_BG;
        boardImg.raycastTarget = false;

        // Slightly lighter inner area to give depth
        var innerImg = UIHelper.CreateImage(_boardContainer, "BoardInner",
            new Vector2(BOARD_SIZE - 24f, BOARD_SIZE - 24f), Vector2.zero, C_BOARD_INNER);
        innerImg.raycastTarget = false;

        // Lines (drawn before nodes so nodes appear on top)
        DrawBoardLines();

        // Nodes
        _nodeImages = new Image[BoardData.NodePositions.Length];
        for (int i = 0; i < BoardData.NodePositions.Length; i++)
            _nodeImages[i] = BuildNode(i);

        // Piece tokens (on-board layer)
        BuildPieceTokens();
    }

    void DrawBoardLines()
    {
        foreach (var (a, b) in BoardData.BoardLines)
        {
            bool isDiag = DiagLines.Contains((a, b)) || DiagLines.Contains((b, a));
            Color lineColor = isDiag ? C_LINE_DIAG : C_LINE_OUTER;
            float thickness = isDiag ? 3f : 4f;

            UIHelper.CreateLine(_boardContainer, BoardData.NodePositions[a],
                                BoardData.NodePositions[b], thickness, lineColor);
        }
    }

    Image BuildNode(int idx)
    {
        bool isCorner = (idx == 5 || idx == 10 || idx == 15);
        bool isStart  = (idx == 0);
        bool isCenter = (idx == 22);

        float r = isCenter ? R_CENTER
                : isStart  ? R_START
                : isCorner ? R_CORNER
                :            R_NORMAL;

        Color fillColor = isStart  ? C_NODE_START
                        : isCenter ? C_NODE_CENTER
                        : isCorner ? C_NODE_CORNER
                        :            C_NODE_NORMAL;

        // Shadow circle (slightly offset, darker)
        var shadowRT = UIHelper.CreateRect(_boardContainer, $"NodeShadow_{idx}",
            new Vector2(r * 2f + 4f, r * 2f + 4f),
            BoardData.NodePositions[idx] + new Vector2(2f, -2f));
        var shadowImg = shadowRT.gameObject.AddComponent<Image>();
        shadowImg.sprite = _circleSprite;
        shadowImg.color  = new Color(0f, 0f, 0f, 0.4f);
        shadowImg.raycastTarget = false;

        // Gold border circle
        var borderRT = UIHelper.CreateRect(_boardContainer, $"NodeBorder_{idx}",
            new Vector2(r * 2f + 4f, r * 2f + 4f), BoardData.NodePositions[idx]);
        var borderImg = borderRT.gameObject.AddComponent<Image>();
        borderImg.sprite = _circleSprite;
        borderImg.color  = C_GOLD;
        borderImg.raycastTarget = false;

        // Fill circle
        var nodeRT = UIHelper.CreateRect(_boardContainer, $"Node_{idx}",
            new Vector2(r * 2f, r * 2f), BoardData.NodePositions[idx]);
        var nodeImg = nodeRT.gameObject.AddComponent<Image>();
        nodeImg.sprite = _circleSprite;
        nodeImg.color  = fillColor;
        nodeImg.raycastTarget = false;

        // Decorations for special nodes
        if (isCorner)
        {
            // Four small satellite circles to suggest a flower/diamond
            Vector2[] offsets = {
                new Vector2(0, r + 5f), new Vector2(0, -(r + 5f)),
                new Vector2(r + 5f, 0), new Vector2(-(r + 5f), 0),
            };
            foreach (var off in offsets)
            {
                var petRT = UIHelper.CreateRect(_boardContainer, $"Petal_{idx}",
                    new Vector2(7f, 7f), BoardData.NodePositions[idx] + off);
                var petImg = petRT.gameObject.AddComponent<Image>();
                petImg.sprite = _circleSprite;
                petImg.color  = C_GOLD_BRIGHT;
                petImg.raycastTarget = false;
            }
        }
        else if (isCenter)
        {
            // Diamond pattern: 4 small circles at 45-degree angles
            float d = r + 4f;
            Vector2[] dOffsets = {
                new Vector2(d, d), new Vector2(-d, d),
                new Vector2(d, -d), new Vector2(-d, -d),
            };
            foreach (var off in dOffsets)
            {
                var dRT = UIHelper.CreateRect(_boardContainer, $"CenterDot",
                    new Vector2(8f, 8f), BoardData.NodePositions[idx] + off);
                var dImg = dRT.gameObject.AddComponent<Image>();
                dImg.sprite = _circleSprite;
                dImg.color  = C_GOLD_BRIGHT;
                dImg.raycastTarget = false;
            }

            UIHelper.CreateText(_boardContainer, "CenterLabel", "中", 16,
                C_GOLD_BRIGHT, new Vector2(30f, 30f), BoardData.NodePositions[idx]);
        }
        else if (isStart)
        {
            // "출발" label
            UIHelper.CreateText(_boardContainer, "StartLabel", "출발", 11,
                Color.white, new Vector2(40f, 28f), BoardData.NodePositions[idx]);
        }

        return nodeImg;
    }

    void BuildPieceTokens()
    {
        _pieceGOs         = new GameObject[2][];
        _pieceImgs        = new Image[2][];
        _pieceRTs         = new RectTransform[2][];
        _pieceShadowGOs   = new GameObject[2][];
        _pieceGlowImgs    = new Image[2][];
        _pieceCountLabels = new TextMeshProUGUI[2][];

        for (int p = 0; p < 2; p++)
        {
            _pieceGOs[p]         = new GameObject[4];
            _pieceImgs[p]        = new Image[4];
            _pieceRTs[p]         = new RectTransform[4];
            _pieceShadowGOs[p]   = new GameObject[4];
            _pieceGlowImgs[p]    = new Image[4];
            _pieceCountLabels[p] = new TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                // Shadow
                var shadowGO = new GameObject($"PieceShadow_P{p}_{i}");
                shadowGO.transform.SetParent(_boardContainer, false);
                var shadowImg = shadowGO.AddComponent<Image>();
                shadowImg.sprite = _circleSprite;
                shadowImg.color  = new Color(0f, 0f, 0f, 0.45f);
                shadowImg.raycastTarget = false;
                var shadowRT = shadowGO.GetComponent<RectTransform>();
                shadowRT.anchorMin = shadowRT.anchorMax = shadowRT.pivot = Vector2.one * 0.5f;
                shadowRT.sizeDelta = new Vector2(PIECE_SIZE + 4f, PIECE_SIZE + 4f);
                _pieceShadowGOs[p][i] = shadowGO;

                // Glow ring (for selection highlight)
                var glowGO = new GameObject($"PieceGlow_P{p}_{i}");
                glowGO.transform.SetParent(_boardContainer, false);
                var glowImg = glowGO.AddComponent<Image>();
                glowImg.sprite = _circleSprite;
                glowImg.color  = new Color(C_HIGHLIGHT.r, C_HIGHLIGHT.g, C_HIGHLIGHT.b, 0f);
                glowImg.raycastTarget = false;
                var glowRT = glowGO.GetComponent<RectTransform>();
                glowRT.anchorMin = glowRT.anchorMax = glowRT.pivot = Vector2.one * 0.5f;
                glowRT.sizeDelta = new Vector2(PIECE_SIZE + 12f, PIECE_SIZE + 12f);
                _pieceGlowImgs[p][i] = glowImg;

                // White outline ring
                var outlineGO = new GameObject($"PieceOutline_P{p}_{i}");
                outlineGO.transform.SetParent(_boardContainer, false);
                var outlineImg = outlineGO.AddComponent<Image>();
                outlineImg.sprite = _circleSprite;
                outlineImg.color  = new Color(1f, 1f, 1f, 0.7f);
                outlineImg.raycastTarget = false;
                var outlineRT = outlineGO.GetComponent<RectTransform>();
                outlineRT.anchorMin = outlineRT.anchorMax = outlineRT.pivot = Vector2.one * 0.5f;
                outlineRT.sizeDelta = new Vector2(PIECE_SIZE + 2f, PIECE_SIZE + 2f);

                // Main piece circle
                var pieceGO = new GameObject($"Piece_P{p}_{i}");
                pieceGO.transform.SetParent(_boardContainer, false);
                var pieceImg = pieceGO.AddComponent<Image>();
                pieceImg.sprite = _circleSprite;
                pieceImg.color  = PieceColor(p);
                pieceImg.raycastTarget = true;

                var pieceRT = pieceGO.GetComponent<RectTransform>();
                pieceRT.anchorMin = pieceRT.anchorMax = pieceRT.pivot = Vector2.one * 0.5f;
                pieceRT.sizeDelta = new Vector2(PIECE_SIZE, PIECE_SIZE);

                // Button for clicking
                var btn = pieceGO.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int cp = p, ci = i;
                btn.onClick.AddListener(() => _ctrl.OnPieceClicked(cp, ci));

                // Stack count badge (small yellow circle + white number)
                var badgeRT = UIHelper.CreateRect(pieceGO.transform, "Badge",
                    new Vector2(16f, 16f), new Vector2(10f, 10f));
                var badgeBg = badgeRT.gameObject.AddComponent<Image>();
                badgeBg.sprite = _circleSprite;
                badgeBg.color  = new Color(1f, 0.85f, 0f, 1f);
                badgeBg.raycastTarget = false;
                var badgeText = UIHelper.CreateText(badgeRT, "BadgeText",
                    "", 11, Color.white, new Vector2(16f, 16f), Vector2.zero);
                _pieceCountLabels[p][i] = badgeText;

                // Lighter highlight dot (inner gleam)
                var gleamRT = UIHelper.CreateRect(pieceGO.transform, "Gleam",
                    new Vector2(10f, 10f), new Vector2(-5f, 5f));
                var gleamImg = gleamRT.gameObject.AddComponent<Image>();
                gleamImg.sprite = _circleSprite;
                gleamImg.color  = new Color(1f, 1f, 1f, 0.35f);
                gleamImg.raycastTarget = false;

                // Store references
                _pieceGOs[p][i]       = pieceGO;
                _pieceImgs[p][i]      = pieceImg;
                _pieceRTs[p][i]       = pieceRT;

                // Keep shadow/outline/glow at the same position (positioned in PositionAllPieces)
                // Store outline RT for positioning
                pieceGO.transform.SetParent(_boardContainer, false); // keep on board

                // Attach shadow+outline+glow as siblings (they follow piece position manually)
                // We'll position them together with the piece each frame via PositionAllPieces.
                // Store the outline GO on the pieceGO via a simple component trick:
                outlineRT.anchoredPosition = Vector2.zero;
                outlineGO.transform.SetParent(pieceGO.transform, false); // child of piece
                shadowRT.anchoredPosition  = new Vector2(2f, -2f);
                shadowGO.transform.SetParent(pieceGO.transform, false);   // child of piece
                glowRT.anchoredPosition    = Vector2.zero;
                glowGO.transform.SetParent(pieceGO.transform, false);    // child of piece

                pieceGO.SetActive(false);
            }
        }
    }

    // -----------------------------------------------------------------------
    //  Build – Bottom bar (yut sticks + throw button + result)
    // -----------------------------------------------------------------------

    void BuildBottomBar()
    {
        var barRT = UIHelper.CreateRect(_canvas.transform, "BottomBar",
                                        new Vector2(CANVAS_W, 80f), new Vector2(0f, -310f));
        var barImg = barRT.gameObject.AddComponent<Image>();
        barImg.sprite = _squareSprite;
        barImg.color  = new Color(0.05f, 0.04f, 0.08f, 0.95f);
        barImg.raycastTarget = false;

        // Gold top border line
        UIHelper.CreateImage(barRT, "BarTopBorder",
            new Vector2(CANVAS_W, 2f), new Vector2(0f, 39f), C_GOLD);

        // --- Yut sticks (4 sticks, left side of bar) ---
        _stickImgs = new Image[4];
        for (int s = 0; s < 4; s++)
        {
            float sx = -350f + s * 25f;
            var stickRT = UIHelper.CreateRect(barRT, $"Stick_{s}",
                new Vector2(12f, 50f), new Vector2(sx, 0f));
            var stickImg = stickRT.gameObject.AddComponent<Image>();
            stickImg.sprite = UIHelper.CreateRoundedRect(12, 50, 5);
            stickImg.type   = Image.Type.Sliced;
            stickImg.color  = new Color(0.90f, 0.80f, 0.60f, 1f); // default: front (앞)
            stickImg.raycastTarget = false;
            _stickImgs[s] = stickImg;
        }

        // "윷 결과" label above sticks
        UIHelper.CreateText(barRT, "SticksLabel", "윷", 11,
            new Color(0.7f, 0.6f, 0.4f, 0.8f),
            new Vector2(100f, 18f), new Vector2(-325f, 35f));

        // --- Throw button (center) ---
        _throwBtn = UIHelper.CreateButton(barRT, "ThrowButton", "던지기",
            24, new Vector2(160f, 56f), new Vector2(20f, 0f),
            C_THROW_BTN, Color.white);
        _throwBtn.onClick.AddListener(_ctrl.OnThrowClicked);
        _throwBtnImg = _throwBtn.GetComponent<Image>();

        // Override button colors for visual feedback
        var colors            = _throwBtn.colors;
        colors.normalColor    = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor   = Color.white;
        colors.disabledColor  = Color.white;
        _throwBtn.colors      = colors;

        _throwBtnLabel = _throwBtn.GetComponentInChildren<TextMeshProUGUI>();

        // --- Throw result text (right of button) ---
        _throwResultLabel = UIHelper.CreateText(barRT, "ThrowResult",
            "", 22, Color.white,
            new Vector2(200f, 50f), new Vector2(220f, 4f));
        _throwResultLabel.fontStyle = FontStyles.Bold;

        // --- Pending throws label ---
        _pendingThrowsLabel = UIHelper.CreateText(barRT, "PendingThrows",
            "", 14, C_GOLD_BRIGHT,
            new Vector2(200f, 20f), new Vector2(220f, -24f));
    }

    // -----------------------------------------------------------------------
    //  Build – Message popup
    // -----------------------------------------------------------------------

    void BuildMessagePopup()
    {
        _msgPopupGO = new GameObject("MessagePopup");
        _msgPopupGO.transform.SetParent(_uiRoot, false);
        var rt = _msgPopupGO.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
        rt.sizeDelta        = new Vector2(360f, 80f);
        rt.anchoredPosition = new Vector2(0f, 60f);

        // Background
        var bgImg = _msgPopupGO.AddComponent<Image>();
        bgImg.sprite = UIHelper.CreateRoundedRect(360, 80, 16);
        bgImg.type   = Image.Type.Sliced;
        bgImg.color  = new Color(0f, 0f, 0f, 0.75f);
        bgImg.raycastTarget = false;

        _msgPopupText = UIHelper.CreateText(rt, "MsgText",
            "", 32, Color.white,
            new Vector2(340f, 70f), Vector2.zero);
        _msgPopupText.fontStyle = FontStyles.Bold;

        _msgPopupGO.SetActive(false);
    }

    // -----------------------------------------------------------------------
    //  Build – Win overlay
    // -----------------------------------------------------------------------

    void BuildWinOverlay()
    {
        _winOverlayGO = new GameObject("WinOverlay");
        _winOverlayGO.transform.SetParent(_uiRoot, false);
        var rt = _winOverlayGO.AddComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.pivot            = Vector2.one * 0.5f;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;

        // Dark semi-transparent overlay
        var bgImg = _winOverlayGO.AddComponent<Image>();
        bgImg.sprite = _squareSprite;
        bgImg.color  = new Color(0f, 0f, 0f, 0.75f);
        bgImg.raycastTarget = true;

        // Win text
        _winOverlayText = UIHelper.CreateText(rt, "WinText",
            "", 52, C_MSG_WIN,
            new Vector2(600f, 120f), new Vector2(0f, 60f));
        _winOverlayText.fontStyle = FontStyles.Bold;

        // Sub-text
        UIHelper.CreateText(rt, "WinSub",
            "다시 시작하려면 새로고침 하세요", 20, Color.white,
            new Vector2(500f, 50f), new Vector2(0f, -20f));

        _winOverlayGO.SetActive(false);
    }

    // -----------------------------------------------------------------------
    //  Public API (called by GameController or externally)
    // -----------------------------------------------------------------------

    /// <summary>Fully refreshes all visuals from current game state.</summary>
    public void RefreshAll()
    {
        PositionAllPieces();
        UpdateUI();
    }

    /// <summary>Syncs all UI labels and interactive states.</summary>
    public void UpdateUI()
    {
        if (_ctrl == null) return;

        // Player panels
        for (int p = 0; p < 2; p++)
        {
            bool isCurrent = _ctrl.CurrentPlayer == p && _ctrl.State != GameController.GameState.GameOver;
            int  finished  = _ctrl.GetFinishedCount(p);

            _playerScoreLabels[p].text = $"완료: {finished}/4";
            _playerTurnLabels[p].text  = isCurrent
                ? (p == 0 ? "당신의 차례" : "AI 생각중...")
                : "";

            // Update home dots colour to reflect piece status
            for (int i = 0; i < 4; i++)
            {
                var piece = _ctrl.Pieces[p][i];
                Color dotColor;
                if (piece.IsFinished(BoardData.Routes))
                    dotColor = new Color(0.8f, 0.8f, 0.8f, 0.4f); // greyed out
                else if (piece.IsHome)
                    dotColor = new Color(PieceColor(p).r, PieceColor(p).g, PieceColor(p).b, 0.55f);
                else
                    dotColor = PieceColor(p); // bright = on board
                _homeDotImgs[p][i].color = dotColor;
            }
        }

        // Throw button state
        bool canThrow = _ctrl.State == GameController.GameState.WaitingThrow
                     && _ctrl.CurrentPlayer == 0;
        _throwBtn.interactable = canThrow;
        _throwBtnImg.color     = canThrow ? C_THROW_BTN : C_THROW_DIM;
        _throwBtnLabel.color   = canThrow ? Color.white  : new Color(0.6f, 0.6f, 0.6f, 1f);

        // Pulse glow on throw button when human can throw
        if (canThrow)
            StartThrowButtonPulse();
        else
            StopThrowButtonPulse();

        // Player panel border pulse
        StartPanelBorderPulse(_ctrl.CurrentPlayer);

        // Pending throws display
        if (_ctrl.PendingThrows.Count > 0 && _ctrl.State != GameController.GameState.GameOver)
        {
            var sb = new System.Text.StringBuilder("남은 이동: ");
            foreach (int t in _ctrl.PendingThrows)
                sb.Append(BoardData.StepsToName(t)).Append(" ");
            _pendingThrowsLabel.text = sb.ToString();
        }
        else
        {
            _pendingThrowsLabel.text = "";
        }
    }

    public void HighlightMovablePieces(int player, int steps)
    {
        ClearGlows();
        var movable = GameLogic.GetMovablePieces(player, _ctrl.Pieces);
        foreach (var piece in movable)
        {
            var test = GameLogic.TryMove(piece, steps, _ctrl.Pieces);
            if (!test.isValid) continue;
            AddGlow(player, piece.pieceId);
        }
    }

    public void HighlightSinglePiece(int player, int pieceId)
    {
        ClearGlows();
        AddGlow(player, pieceId);
    }

    // -----------------------------------------------------------------------
    //  Event handlers
    // -----------------------------------------------------------------------

    void HandleThrowResult(int steps)
    {
        StartCoroutine(AnimateThrow(steps));
    }

    void HandlePieceMove(int player, int pieceId, int fromNode, int toNode)
    {
        ClearGlows();
        StartCoroutine(AnimatePieceMove(player, pieceId, fromNode, toNode));
    }

    void HandleCapture(int capturingPlayer, int node)
    {
        StartCoroutine(AnimateCaptureMessage(capturingPlayer));
    }

    void HandleStack(int player, int node)
    {
        ShowPopupMessage("업었다!", C_MSG_STACK);
    }

    void HandleWin(int winner)
    {
        ShowWinOverlay(winner);
    }

    void HandleTurnChange(int newPlayer)
    {
        ClearGlows();
        UpdateUI();
    }

    void HandleBonusThrow()
    {
        ShowPopupMessage("보너스 던지기!", C_GOLD_BRIGHT);
    }

    void HandlePieceFinished(int player, int pieceId)
    {
        ShowPopupMessage("도착!", C_MSG_ARRIVE);
    }

    // -----------------------------------------------------------------------
    //  Piece positioning
    // -----------------------------------------------------------------------

    void PositionAllPieces()
    {
        if (_ctrl == null || _pieceGOs == null) return;

        // Collect node occupants for offset calculation
        var nodeOccupants = new Dictionary<int, List<(int p, int i)>>();
        for (int p = 0; p < 2; p++)
        for (int i = 0; i < 4; i++)
        {
            var piece = _ctrl.Pieces[p][i];
            int node  = piece.CurrentNode(BoardData.Routes);
            if (node < 0) continue;
            if (!nodeOccupants.ContainsKey(node))
                nodeOccupants[node] = new List<(int, int)>();
            nodeOccupants[node].Add((p, i));
        }

        int[] homeSlot = { 0, 0 };

        for (int p = 0; p < 2; p++)
        for (int i = 0; i < 4; i++)
        {
            var piece = _ctrl.Pieces[p][i];
            var go    = _pieceGOs[p][i];
            var rt    = _pieceRTs[p][i];

            if (piece.IsFinished(BoardData.Routes))
            {
                // Show as dimmed token in the home area (right of panel dots)
                go.SetActive(true);
                float fx = (p == 0 ? -315f : 205f) + (homeSlot[p] % 4) * 22f;
                rt.anchoredPosition = new Vector2(fx, -300f);
                rt.sizeDelta        = new Vector2(PIECE_SIZE_HOME, PIECE_SIZE_HOME);
                _pieceImgs[p][i].color = new Color(PieceColor(p).r, PieceColor(p).g,
                                                   PieceColor(p).b, 0.4f);
                _pieceCountLabels[p][i].text = "";
                _pieceGlowImgs[p][i].color = Color.clear;
                homeSlot[p]++;
                continue;
            }

            if (piece.IsHome)
            {
                // Off-board: hide the on-board token
                go.SetActive(false);
                continue;
            }

            // On-board piece
            int     node     = piece.CurrentNode(BoardData.Routes);
            Vector2 basePos  = BoardData.NodePositions[node];

            var occupants   = nodeOccupants.ContainsKey(node) ? nodeOccupants[node] : null;
            int slotIdx     = 0;
            int totalAtNode = occupants?.Count ?? 1;

            if (occupants != null)
                for (int k = 0; k < occupants.Count; k++)
                    if (occupants[k].p == p && occupants[k].i == i) { slotIdx = k; break; }

            // Spiral offset to separate pieces at the same node
            Vector2 offset = ComputeSlotOffset(slotIdx, totalAtNode);

            go.SetActive(true);
            rt.sizeDelta        = new Vector2(PIECE_SIZE, PIECE_SIZE);
            rt.anchoredPosition = basePos + offset;
            _pieceImgs[p][i].color = PieceColor(p);

            // Count friendly pieces at same node
            int friendCount = 0;
            bool isFirstOfPlayer = true;
            if (occupants != null)
            {
                foreach (var occ in occupants)
                    if (occ.p == p) { friendCount++; if (occ.i < i) isFirstOfPlayer = false; }
            }

            // Stack badge
            bool showBadge = friendCount > 1 && isFirstOfPlayer;
            _pieceCountLabels[p][i].text = showBadge ? friendCount.ToString() : "";
            var badgeRT = _pieceCountLabels[p][i].rectTransform.parent.GetComponent<RectTransform>();
            if (badgeRT != null) badgeRT.gameObject.SetActive(showBadge);
        }
    }

    static Vector2 ComputeSlotOffset(int slotIdx, int total)
    {
        if (total <= 1) return Vector2.zero;
        // Arrange in a small circle around the node centre
        float angle  = (360f / total) * slotIdx * Mathf.Deg2Rad;
        float radius = total <= 3 ? 10f : 14f;
        return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }

    // -----------------------------------------------------------------------
    //  Glow (selection highlight)
    // -----------------------------------------------------------------------

    void AddGlow(int player, int pieceId)
    {
        var set = player == 0 ? _glowP0 : _glowP1;
        set.Add(pieceId);
        int idx = player * 4 + pieceId;
        if (_glowCoroutines[idx] != null) StopCoroutine(_glowCoroutines[idx]);
        _glowCoroutines[idx] = StartCoroutine(GlowPulse(player, pieceId));
    }

    void ClearGlows()
    {
        _glowP0.Clear();
        _glowP1.Clear();
        for (int idx = 0; idx < 8; idx++)
        {
            if (_glowCoroutines[idx] != null) { StopCoroutine(_glowCoroutines[idx]); _glowCoroutines[idx] = null; }
        }
        for (int p = 0; p < 2; p++)
        for (int i = 0; i < 4; i++)
        {
            if (_pieceGlowImgs[p][i] != null)
                _pieceGlowImgs[p][i].color = Color.clear;
            if (_pieceGOs[p][i] != null)
                _pieceGOs[p][i].transform.localScale = Vector3.one;
        }
    }

    IEnumerator GlowPulse(int player, int pieceId)
    {
        var glowImg = _pieceGlowImgs[player][pieceId];
        var goTr    = _pieceGOs[player][pieceId].transform;
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 2.5f;
            float alpha = (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f;
            float scale = 1f + alpha * 0.12f;
            if (glowImg != null)
                glowImg.color = new Color(C_HIGHLIGHT.r, C_HIGHLIGHT.g, C_HIGHLIGHT.b, alpha * 0.85f);
            if (goTr != null)
                goTr.localScale = Vector3.one * scale;
            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    //  Throw button pulse
    // -----------------------------------------------------------------------

    void StartThrowButtonPulse()
    {
        if (_throwBtnPulseCoroutine != null) return;
        _throwBtnPulseCoroutine = StartCoroutine(ThrowButtonPulse());
    }

    void StopThrowButtonPulse()
    {
        if (_throwBtnPulseCoroutine != null)
        {
            StopCoroutine(_throwBtnPulseCoroutine);
            _throwBtnPulseCoroutine = null;
        }
        if (_throwBtnImg != null) _throwBtnImg.color = C_THROW_DIM;
    }

    IEnumerator ThrowButtonPulse()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 1.8f;
            float alpha = (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f;
            Color c = Color.Lerp(C_THROW_BTN, C_GOLD_BRIGHT, alpha * 0.6f);
            if (_throwBtnImg != null) _throwBtnImg.color = c;
            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    //  Player panel border pulse
    // -----------------------------------------------------------------------

    void StartPanelBorderPulse(int player)
    {
        if (_panelBorderPulseCoroutine != null)
        {
            StopCoroutine(_panelBorderPulseCoroutine);
            _panelBorderPulseCoroutine = null;
        }
        // Clear opposite panel border
        int other = 1 - player;
        if (_playerPanelBorders[other] != null)
            _playerPanelBorders[other].color = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, 0f);

        if (_ctrl.State == GameController.GameState.GameOver) return;
        _panelBorderPulseCoroutine = StartCoroutine(PanelBorderPulse(player));
    }

    IEnumerator PanelBorderPulse(int player)
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 1.5f;
            float alpha = (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f;
            if (_playerPanelBorders[player] != null)
                _playerPanelBorders[player].color = new Color(C_GOLD.r, C_GOLD.g, C_GOLD.b, alpha * 0.9f);
            yield return null;
        }
    }

    // -----------------------------------------------------------------------
    //  Animation: throw sticks
    // -----------------------------------------------------------------------

    IEnumerator AnimateThrow(int steps)
    {
        // Rapidly flip sticks back and forth for 0.5 seconds
        float elapsed = 0f;
        float flipDuration = 0.55f;
        Color frontColor = new Color(0.90f, 0.80f, 0.60f, 1f);
        Color backColor  = new Color(0.30f, 0.20f, 0.10f, 1f);

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            for (int s = 0; s < 4; s++)
            {
                float phase = Mathf.Sin((elapsed * 12f + s * 1.5f) * Mathf.PI);
                if (_stickImgs[s] != null)
                    _stickImgs[s].color = phase > 0f ? frontColor : backColor;
            }
            yield return null;
        }

        // Settle to correct result
        // steps: 모=5 (0 flat/front), 도=1 (1 front), 개=2 (2 front), 걸=3 (3 front), 윷=4 (4 front)
        int frontCount = steps == 5 ? 0 : steps; // number of sticks showing front
        for (int s = 0; s < 4; s++)
        {
            if (_stickImgs[s] != null)
                _stickImgs[s].color = (s < frontCount) ? frontColor : backColor;
        }

        // Animate scale bump on sticks
        for (int s = 0; s < 4; s++)
        {
            if (_stickImgs[s] != null)
            {
                var stickTr = _stickImgs[s].transform;
                StartCoroutine(ScaleBump(stickTr, 1.15f, 0.15f));
            }
        }

        // Display result text
        string resultName = BoardData.StepsToName(steps);
        bool isBonus      = GameLogic.GivesBonus(steps);
        Color resultColor = isBonus ? C_GOLD_BRIGHT : Color.white;

        _throwResultLabel.text  = resultName;
        _throwResultLabel.color = resultColor;

        yield return StartCoroutine(ScaleBump(_throwResultLabel.transform, 1.3f, 0.25f));

        if (isBonus)
        {
            yield return new WaitForSeconds(0.1f);
            ShowPopupMessage($"{resultName}! 한 번 더!", C_GOLD_BRIGHT);
        }

        RefreshAll();
    }

    // -----------------------------------------------------------------------
    //  Animation: piece movement with arc
    // -----------------------------------------------------------------------

    IEnumerator AnimatePieceMove(int player, int pieceId, int fromNode, int toNode)
    {
        if (toNode < 0 || fromNode < 0)
        {
            // Piece entering board or finishing – snap to position, then refresh
            yield return new WaitForSeconds(0.05f);
            RefreshAll();
            yield break;
        }

        var go = _pieceGOs[player][pieceId];
        var rt = _pieceRTs[player][pieceId];

        if (go == null || !go.activeSelf)
        {
            RefreshAll();
            yield break;
        }

        Vector2 startPos = BoardData.NodePositions[fromNode];
        Vector2 endPos   = BoardData.NodePositions[toNode];
        float   arcHeight = Mathf.Max(20f, Vector2.Distance(startPos, endPos) * 0.25f);
        float   duration  = 0.4f;

        go.SetActive(true);
        rt.anchoredPosition = startPos;

        yield return StartCoroutine(UIHelper.AnimateFloat(0f, 1f, duration, t =>
        {
            Vector2 pos = Vector2.Lerp(startPos, endPos, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            if (rt != null) rt.anchoredPosition = pos;
        }));

        RefreshAll();
    }

    // -----------------------------------------------------------------------
    //  Animation: capture flash
    // -----------------------------------------------------------------------

    IEnumerator AnimateCaptureMessage(int capturingPlayer)
    {
        ShowPopupMessage("잡았다!", C_MSG_CAPTURE);
        // Flash the board with a brief red tint
        var flashImg = UIHelper.CreateImage(_boardContainer, "CaptureFlash",
            new Vector2(BOARD_SIZE, BOARD_SIZE), Vector2.zero,
            new Color(1f, 0.1f, 0.1f, 0f));
        flashImg.raycastTarget = false;

        yield return StartCoroutine(UIHelper.AnimateFloat(0f, 0.35f, 0.1f,
            a => { if (flashImg != null) flashImg.color = new Color(1f, 0.1f, 0.1f, a); }));
        yield return StartCoroutine(UIHelper.AnimateFloat(0.35f, 0f, 0.25f,
            a => { if (flashImg != null) flashImg.color = new Color(1f, 0.1f, 0.1f, a); }));

        if (flashImg != null) Destroy(flashImg.gameObject);
        RefreshAll();
    }

    // -----------------------------------------------------------------------
    //  Popup message (appears at board centre, fades out)
    // -----------------------------------------------------------------------

    void ShowPopupMessage(string message, Color color)
    {
        StartCoroutine(PopupMessageCoroutine(message, color));
    }

    IEnumerator PopupMessageCoroutine(string message, Color color)
    {
        _msgPopupGO.SetActive(true);
        _msgPopupText.text  = message;
        _msgPopupText.color = color;

        var rt = _msgPopupGO.GetComponent<RectTransform>();

        // Scale in: 0 → 1.2 → 1.0
        rt.localScale = Vector3.zero;
        float scaleDuration = 0.2f;
        yield return StartCoroutine(UIHelper.AnimateFloat(0f, 1.2f, scaleDuration,
            s => rt.localScale = Vector3.one * s));
        yield return StartCoroutine(UIHelper.AnimateFloat(1.2f, 1.0f, 0.08f,
            s => rt.localScale = Vector3.one * s));

        // Hold
        yield return new WaitForSeconds(0.8f);

        // Fade out while drifting up
        Vector2 startPos = rt.anchoredPosition;
        yield return StartCoroutine(UIHelper.AnimateFloat(1f, 0f, 0.4f, a =>
        {
            _msgPopupText.color = new Color(color.r, color.g, color.b, a);
            rt.anchoredPosition = Vector2.Lerp(startPos, startPos + new Vector2(0f, 40f),
                                               1f - a);
        }));

        rt.anchoredPosition = new Vector2(0f, 60f);
        _msgPopupGO.SetActive(false);
    }

    // -----------------------------------------------------------------------
    //  Win overlay
    // -----------------------------------------------------------------------

    void ShowWinOverlay(int winner)
    {
        _winOverlayGO.SetActive(true);
        _winOverlayText.text  = winner == 0 ? "사람 승리!" : "AI 승리!";
        _winOverlayText.color = winner == 0 ? C_P0_PIECE : C_P1_PIECE;
        StartCoroutine(WinOverlayAnimation());
    }

    IEnumerator WinOverlayAnimation()
    {
        var rt = _winOverlayGO.GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;

        var bgImg = _winOverlayGO.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0f);

        yield return StartCoroutine(UIHelper.AnimateFloat(0f, 1f, 0.4f, t =>
        {
            rt.localScale   = Vector3.one * t;
            bgImg.color = new Color(0f, 0f, 0f, t * 0.75f);
        }));

        // Celebratory scale pulse
        for (int k = 0; k < 3; k++)
        {
            yield return StartCoroutine(UIHelper.AnimateFloat(1f, 1.06f, 0.18f,
                s => _winOverlayText.transform.localScale = Vector3.one * s));
            yield return StartCoroutine(UIHelper.AnimateFloat(1.06f, 1f, 0.18f,
                s => _winOverlayText.transform.localScale = Vector3.one * s));
        }
    }

    // -----------------------------------------------------------------------
    //  Generic animation helpers
    // -----------------------------------------------------------------------

    IEnumerator ScaleBump(Transform target, float peakScale, float duration)
    {
        if (target == null) yield break;
        yield return StartCoroutine(UIHelper.AnimateFloat(1f, peakScale, duration * 0.5f,
            s => { if (target != null) target.localScale = Vector3.one * s; }));
        yield return StartCoroutine(UIHelper.AnimateFloat(peakScale, 1f, duration * 0.5f,
            s => { if (target != null) target.localScale = Vector3.one * s; }));
    }

    // -----------------------------------------------------------------------
    //  Sprite utility
    // -----------------------------------------------------------------------

    static Sprite MakeWhiteSquare()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
    }
}
