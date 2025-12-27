// ChessPositionCSVParser.cs
// Supports:
// - FEN (most common chess position format)
// - CSV list: square,piece   OR  square,color,type
// - CSV 8x8 grid: 8 lines x 8 columns using piece letters (P,R,N,B,Q,K / lowercase for black), '.' for empty
//
// CHANGE: "Forget load from file".
// This version ONLY loads from a TextAsset, but it also provides an Editor-only dropdown that scans a folder
// (e.g. Assets/Data/Positions) and lets you pick one TextAsset from that folder in the Inspector.
//
// Integration:
// - ChessGameController can call LoadAndApply(clearOldPiecesOverride:false, controller:this)
// - Side-to-move from FEN is applied safely via controller.SetPendingSideToMoveOverride(...)

using System;
using System.Collections.Generic;
using System.IO; // StringReader
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChessPositionCSVParser : MonoBehaviour
{
    public enum InputFormat
    {
        Auto,
        FEN,
        CSV_List,
        CSV_Grid8x8
    }

    [Header("References")]
    public PieceInitializer pieceInitializer;
    public ChessGameController gameController;

    // ------------------------------------------------------------------
    // NEW: Folder scan + dropdown (Editor-only)
    // ------------------------------------------------------------------
    [Header("Position Asset Picker (Editor-only scan)")]
    [Tooltip("Folder under Assets/. Example: Assets/Data/Positions")]
    public string folder = "Assets/Data";

    [Tooltip("If enabled, updates the dropdown list when you change values in Inspector.")]
    public bool autoRefreshInEditor = true;

    [Tooltip("Pick a position text asset (FEN/CSV). This is what will be parsed.")]
    public TextAsset textAsset;

#if UNITY_EDITOR
    [SerializeField, HideInInspector] private List<TextAsset> options = new List<TextAsset>();
    [SerializeField, HideInInspector] private int selectedIndex = -1;
#endif

    [Header("Parsing")]
    public InputFormat format = InputFormat.Auto;

    [Tooltip("If true, clears existing pieces before applying this position (when calling LoadAndApply() with no override).")]
    public bool clearOldPieces = true;

    [Tooltip("If true and FEN includes side-to-move, set ChessGameController.sideToMove accordingly (applied safely).")]
    public bool applySideToMoveFromFEN = true;

    [Header("Run")]
    public bool loadOnStart = false;

    // --- Unity 2023+ safe find helper (avoids CS0618 warnings) ---
    static T FindFirst<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }

    void Awake()
    {
        if (pieceInitializer == null) pieceInitializer = FindFirst<PieceInitializer>();
        if (gameController == null) gameController = FindFirst<ChessGameController>();
    }

    void Start()
    {
        if (loadOnStart)
            LoadAndApply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoRefreshInEditor) return;

        // Keep the dropdown list current when folder changes.
        RefreshOptions();
        SyncIndexFromTextAsset();
    }

    [ContextMenu("Refresh Position Options (Editor)")]
    public void RefreshOptions()
    {
        options.Clear();
        selectedIndex = -1;

        if (string.IsNullOrWhiteSpace(folder)) return;

        // Find all TextAssets in folder (including subfolders).
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (ta != null) options.Add(ta);
        }

        options.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        SyncIndexFromTextAsset();
    }

    void SyncIndexFromTextAsset()
    {
        if (textAsset == null) { selectedIndex = -1; return; }
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == textAsset)
            {
                selectedIndex = i;
                return;
            }
        }
        selectedIndex = -1;
    }

    void SyncTextAssetFromIndex()
    {
        if (selectedIndex < 0 || selectedIndex >= options.Count) return;
        textAsset = options[selectedIndex];
    }
#endif

    [ContextMenu("Load And Apply Position")]
    public void LoadAndApply()
    {
        LoadAndApply(clearOldPiecesOverride: clearOldPieces, controller: gameController);
    }

    /// <summary>
    /// Controller-friendly overload:
    /// - clearOldPiecesOverride: pass false if the controller already cleared pieces.
    /// - controller: used to safely apply side-to-move after PieceInitializer fires OnPiecesInitialized.
    /// </summary>
    public void LoadAndApply(bool clearOldPiecesOverride, ChessGameController controller)
    {
        if (pieceInitializer == null)
        {
            Debug.LogError("ChessPositionCSVParser: pieceInitializer is not set.");
            return;
        }

        string text = GetInputText();
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogError("ChessPositionCSVParser: textAsset is not set or is empty.");
            return;
        }

        InputFormat chosen = (format == InputFormat.Auto) ? AutoDetect(text) : format;

        List<PieceInitializer.PiecePlacement> placements;
        PieceColor? sideToMove = null;

        try
        {
            switch (chosen)
            {
                case InputFormat.FEN:
                    placements = ParseFEN(text, out sideToMove);
                    break;

                case InputFormat.CSV_Grid8x8:
                    placements = ParseCSVGrid8x8(text);
                    break;

                case InputFormat.CSV_List:
                default:
                    placements = ParseCSVList(text);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ChessPositionCSVParser: parse failed: {ex.Message}\n{ex.StackTrace}");
            return;
        }

        // Avoid side-to-move race: GameController should apply this after pieces finish spawning.
        if (applySideToMoveFromFEN && controller != null)
            controller.SetPendingSideToMoveOverride(sideToMove);

        pieceInitializer.InitializeFromPlacements(placements, clearOldPiecesOverride);
    }

    // ------------------------------------------------------------------
    // Text source (TextAsset only)
    // ------------------------------------------------------------------

    string GetInputText()
    {
        return (textAsset != null) ? textAsset.text : null;
    }

    // ------------------------------------------------------------------
    // Auto detect
    // ------------------------------------------------------------------

    InputFormat AutoDetect(string text)
    {
        string t = text.Trim();

        // FEN typically contains 7+ slashes in the first field.
        int slashCount = 0;
        for (int i = 0; i < t.Length; i++)
            if (t[i] == '/') slashCount++;

        if (slashCount >= 7 && ContainsAnyPieceLetter(t))
            return InputFormat.FEN;

        // Try grid: 8 lines, each with 8 tokens (comma/semicolon/whitespace separated).
        var lines = SplitLines(t);
        if (lines.Count == 8)
        {
            bool looksGrid = true;
            for (int i = 0; i < 8; i++)
            {
                var tokens = SplitCSVTokens(lines[i]);
                if (tokens.Count != 8) { looksGrid = false; break; }
            }
            if (looksGrid) return InputFormat.CSV_Grid8x8;
        }

        return InputFormat.CSV_List;
    }

    bool ContainsAnyPieceLetter(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if ("prnbqkPRNBQK".IndexOf(c) >= 0) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // FEN
    // ------------------------------------------------------------------

    List<PieceInitializer.PiecePlacement> ParseFEN(string fenText, out PieceColor? sideToMove)
    {
        // Accept extra whitespace/newlines; take first non-empty line.
        string fen = FirstNonEmptyLine(fenText);
        if (string.IsNullOrWhiteSpace(fen))
            throw new Exception("FEN is empty.");

        // Fields: piecePlacement sideToMove castling enPassant halfmove fullmove
        string[] fields = fen.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 1)
            throw new Exception("Invalid FEN: missing piece placement field.");

        string placement = fields[0];

        sideToMove = null;
        if (fields.Length >= 2)
        {
            if (fields[1] == "w") sideToMove = PieceColor.White;
            else if (fields[1] == "b") sideToMove = PieceColor.Black;
        }

        string[] ranks = placement.Split('/');
        if (ranks.Length != 8)
            throw new Exception("Invalid FEN: piece placement must have 8 ranks separated by '/'.");

        var res = new List<PieceInitializer.PiecePlacement>(64);

        // FEN ranks are 8->1, files a->h
        for (int rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            string rank = ranks[rankIndex];
            int file = 0; // 0..7 => A..H
            int boardRank = 8 - rankIndex; // 8..1

            for (int i = 0; i < rank.Length; i++)
            {
                char c = rank[i];

                if (char.IsDigit(c))
                {
                    int empty = c - '0';
                    file += empty;
                    continue;
                }

                if (!TryFenPieceToPlacement(c, out PieceColor color, out PieceType type))
                    throw new Exception($"Invalid FEN: unknown piece char '{c}'.");

                if (file < 0 || file > 7)
                    throw new Exception("Invalid FEN: file out of bounds while parsing rank.");

                string square = $"{(char)('A' + file)}{boardRank}";
                res.Add(new PieceInitializer.PiecePlacement(square, color, type));
                file++;
            }

            if (file != 8)
                throw new Exception($"Invalid FEN: rank {boardRank} does not sum to 8 files.");
        }

        return res;
    }

    bool TryFenPieceToPlacement(char c, out PieceColor color, out PieceType type)
    {
        color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;

        char lc = char.ToLowerInvariant(c);
        switch (lc)
        {
            case 'p': type = PieceType.Pawn; return true;
            case 'r': type = PieceType.Rook; return true;
            case 'n': type = PieceType.Knight; return true;
            case 'b': type = PieceType.Bishop; return true;
            case 'q': type = PieceType.Queen; return true;
            case 'k': type = PieceType.King; return true;
        }

        type = PieceType.Pawn;
        return false;
    }

    // ------------------------------------------------------------------
    // CSV list
    // Supported examples:
    //   A1,WR
    //   E8,Black,King
    //   D1,White,Queen
    // Comments: lines starting with '#' or '//' are ignored.
    // ------------------------------------------------------------------

    List<PieceInitializer.PiecePlacement> ParseCSVList(string text)
    {
        var lines = SplitLines(text);
        var res = new List<PieceInitializer.PiecePlacement>(64);

        for (int li = 0; li < lines.Count; li++)
        {
            string raw = lines[li].Trim();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (raw.StartsWith("#") || raw.StartsWith("//")) continue;

            var tokens = SplitCSVTokens(raw);
            if (tokens.Count == 0) continue;

            // allow header line
            if (li == 0 && tokens.Count >= 2 && tokens[0].Equals("square", StringComparison.OrdinalIgnoreCase))
                continue;

            if (tokens.Count == 2)
            {
                string square = NormalizeSquare(tokens[0]);
                if (!IsSquare(square))
                    throw new Exception($"CSV line {li + 1}: invalid square '{tokens[0]}'.");

                if (!TryParsePieceCode(tokens[1], out PieceColor color, out PieceType type))
                    throw new Exception($"CSV line {li + 1}: invalid piece code '{tokens[1]}'.");

                res.Add(new PieceInitializer.PiecePlacement(square, color, type));
            }
            else if (tokens.Count >= 3)
            {
                string square = NormalizeSquare(tokens[0]);
                if (!IsSquare(square))
                    throw new Exception($"CSV line {li + 1}: invalid square '{tokens[0]}'.");

                if (!TryParseColor(tokens[1], out PieceColor color))
                    throw new Exception($"CSV line {li + 1}: invalid color '{tokens[1]}'.");

                if (!TryParseType(tokens[2], out PieceType type))
                    throw new Exception($"CSV line {li + 1}: invalid type '{tokens[2]}'.");

                res.Add(new PieceInitializer.PiecePlacement(square, color, type));
            }
            else
            {
                throw new Exception($"CSV line {li + 1}: expected 2 or 3+ columns.");
            }
        }

        return res;
    }

    bool TryParsePieceCode(string codeRaw, out PieceColor color, out PieceType type)
    {
        // Accept:
        // - Single FEN letter: P/p/R/r...
        // - Prefix color + piece: "WP", "BK", "wQ", "bN"
        // - Words: "WhiteQueen", "BlackRook" (best-effort)
        string code = (codeRaw ?? "").Trim();
        if (code.Length == 0) { color = PieceColor.White; type = PieceType.Pawn; return false; }

        // Single FEN letter
        if (code.Length == 1 && "prnbqkPRNBQK".IndexOf(code[0]) >= 0)
            return TryFenPieceToPlacement(code[0], out color, out type);

        // If starts with w/b/W/B, next char is piece letter
        if (code.Length >= 2)
        {
            char c0 = code[0];
            char c1 = code[1];

            if (c0 == 'w' || c0 == 'W' || c0 == 'b' || c0 == 'B')
            {
                color = (c0 == 'w' || c0 == 'W') ? PieceColor.White : PieceColor.Black;

                char pieceChar = c1;
                char fen = (color == PieceColor.White)
                    ? char.ToUpperInvariant(pieceChar)
                    : char.ToLowerInvariant(pieceChar);

                return TryFenPieceToPlacement(fen, out color, out type);
            }
        }

        // Last resort: parse words
        string lower = code.ToLowerInvariant();
        if (lower.Contains("white")) color = PieceColor.White;
        else if (lower.Contains("black")) color = PieceColor.Black;
        else color = PieceColor.White;

        if (lower.Contains("pawn")) { type = PieceType.Pawn; return true; }
        if (lower.Contains("rook")) { type = PieceType.Rook; return true; }
        if (lower.Contains("knight")) { type = PieceType.Knight; return true; }
        if (lower.Contains("bishop")) { type = PieceType.Bishop; return true; }
        if (lower.Contains("queen")) { type = PieceType.Queen; return true; }
        if (lower.Contains("king")) { type = PieceType.King; return true; }

        type = PieceType.Pawn;
        return false;
    }

    bool TryParseColor(string raw, out PieceColor color)
    {
        string s = (raw ?? "").Trim().ToLowerInvariant();
        if (s == "w" || s == "white") { color = PieceColor.White; return true; }
        if (s == "b" || s == "black") { color = PieceColor.Black; return true; }
        color = PieceColor.White;
        return false;
    }

    bool TryParseType(string raw, out PieceType type)
    {
        string s = (raw ?? "").Trim().ToLowerInvariant();
        if (s == "p" || s == "pawn") { type = PieceType.Pawn; return true; }
        if (s == "r" || s == "rook") { type = PieceType.Rook; return true; }
        if (s == "n" || s == "knight") { type = PieceType.Knight; return true; }
        if (s == "b" || s == "bishop") { type = PieceType.Bishop; return true; }
        if (s == "q" || s == "queen") { type = PieceType.Queen; return true; }
        if (s == "k" || s == "king") { type = PieceType.King; return true; }

        type = PieceType.Pawn;
        return false;
    }

    // ------------------------------------------------------------------
    // CSV 8x8 grid
    // Line 1 = rank 8, line 8 = rank 1.
    // ------------------------------------------------------------------

    List<PieceInitializer.PiecePlacement> ParseCSVGrid8x8(string text)
    {
        var lines = SplitLines(text);
        if (lines.Count != 8)
            throw new Exception("CSV_Grid8x8 expects exactly 8 lines (rank 8 to rank 1).");

        var res = new List<PieceInitializer.PiecePlacement>(64);

        for (int row = 0; row < 8; row++)
        {
            int rank = 8 - row;
            var cells = SplitCSVTokens(lines[row]);

            if (cells.Count != 8)
                throw new Exception($"CSV_Grid8x8 line {row + 1}: expected 8 columns, got {cells.Count}.");

            for (int col = 0; col < 8; col++)
            {
                int file = col; // A..H
                string cell = (cells[col] ?? "").Trim();
                if (string.IsNullOrEmpty(cell) || cell == "." || cell == "0" || cell == "-")
                    continue;

                if (!TryParsePieceCode(cell, out PieceColor color, out PieceType type))
                    throw new Exception($"CSV_Grid8x8 line {row + 1}, col {col + 1}: invalid piece '{cell}'.");

                string square = $"{(char)('A' + file)}{rank}";
                res.Add(new PieceInitializer.PiecePlacement(square, color, type));
            }
        }

        return res;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static List<string> SplitLines(string text)
    {
        var res = new List<string>(64);
        using (var sr = new StringReader(text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
                res.Add(line);
        }
        return res;
    }

    static string FirstNonEmptyLine(string text)
    {
        using (var sr = new StringReader(text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    return line.Trim();
            }
        }
        return null;
    }

    // Simple CSV split (commas/semicolons). If no delimiters, falls back to whitespace split.
    static List<string> SplitCSVTokens(string line)
    {
        var res = new List<string>(16);
        if (line == null) return res;

        string[] parts = line.Split(new[] { ',', ';' }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
            res.Add(parts[i].Trim());

        // Fallback: if single token and has spaces, treat as whitespace-separated
        if (res.Count == 1)
        {
            string t = res[0];
            res.Clear();
            var ws = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < ws.Length; i++)
                res.Add(ws[i].Trim());
        }

        return res;
    }

    static string NormalizeSquare(string sq)
    {
        return (sq ?? "").Trim().ToUpperInvariant();
    }

    static bool IsSquare(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 2 || s.Length > 3) return false;
        char f = s[0];
        if (f < 'A' || f > 'H') return false;
        if (!int.TryParse(s.Substring(1), out int r)) return false;
        return r >= 1 && r <= 8;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ChessPositionCSVParser))]
public class ChessPositionCSVParserEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (ChessPositionCSVParser)target;

        // Draw default fields except our custom picker UI (we still show textAsset field too, but synced)
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        t.pieceInitializer = (PieceInitializer)EditorGUILayout.ObjectField("Piece Initializer", t.pieceInitializer, typeof(PieceInitializer), true);
        t.gameController = (ChessGameController)EditorGUILayout.ObjectField("Game Controller", t.gameController, typeof(ChessGameController), true);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Position Asset Picker (Editor-only scan)", EditorStyles.boldLabel);

        t.folder = EditorGUILayout.TextField(new GUIContent("Folder (Assets/...)"), t.folder);
        t.autoRefreshInEditor = EditorGUILayout.Toggle("Auto Refresh In Editor", t.autoRefreshInEditor);

        if (GUILayout.Button("Refresh Options"))
        {
            t.RefreshOptions();
            EditorUtility.SetDirty(t);
        }

        serializedObject.Update();

        SerializedProperty optionsProp = serializedObject.FindProperty("options");
        SerializedProperty idxProp = serializedObject.FindProperty("selectedIndex");
        SerializedProperty textAssetProp = serializedObject.FindProperty("textAsset");

        string[] names = new string[optionsProp.arraySize];
        for (int i = 0; i < optionsProp.arraySize; i++)
        {
            var obj = optionsProp.GetArrayElementAtIndex(i).objectReferenceValue as TextAsset;
            names[i] = obj ? obj.name : "(null)";
        }

        int idx = idxProp.intValue;
        int newIdx = (names.Length > 0)
            ? EditorGUILayout.Popup("Selected Position", Mathf.Clamp(idx, 0, Mathf.Max(0, names.Length - 1)), names)
            : EditorGUILayout.Popup("Selected Position", -1, new string[] { "(no TextAssets found)" });

        if (names.Length > 0 && newIdx != idx)
        {
            idxProp.intValue = newIdx;
            textAssetProp.objectReferenceValue = optionsProp.GetArrayElementAtIndex(newIdx).objectReferenceValue;
        }

        // Also allow manual override (keeps script usable even without folder scan)
        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(textAssetProp, new GUIContent("Text Asset (manual)"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Parsing", EditorStyles.boldLabel);
        t.format = (ChessPositionCSVParser.InputFormat)EditorGUILayout.EnumPopup("Format", t.format);
        t.clearOldPieces = EditorGUILayout.Toggle("Clear Old Pieces", t.clearOldPieces);
        t.applySideToMoveFromFEN = EditorGUILayout.Toggle("Apply Side To Move From FEN", t.applySideToMoveFromFEN);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Run", EditorStyles.boldLabel);
        t.loadOnStart = EditorGUILayout.Toggle("Load On Start", t.loadOnStart);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Load And Apply Now"))
        {
            t.LoadAndApply();
        }

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            EditorUtility.SetDirty(t);
    }
}
#endif
