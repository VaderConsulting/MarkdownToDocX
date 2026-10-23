namespace LiveEditor;

/// <summary>
/// Per-letter feature switches for LiveEditor (A-AD). Flip one at a time when committing.
/// </summary>
internal static class LiveEditorFeatureGate
{
    public static bool A_SyntaxHighlighting = true;

    public static bool B_ElementTypeCompletion = true;

    public static bool C_QualifierCompletion = true;

    public static bool D_PlacementHintCompletion = true;

    public static bool E_InlineDiagnostics = true;

    public static bool F_GhostText = true;

    public static bool G_SignatureHelp = true;

    public static bool H_GoToDefinition = true;

    public static bool I_RenameSymbol = true;

    public static bool J_Snippets = true;

    public static bool K_SampleGallery = true;

    public static bool L_ZoomPanPreview = true;

    public static bool M_ExportPngSvg = true;

    public static bool N_CopySource = true;

    public static bool O_ThemeToggle = true;

    public static bool P_SplitOrientation = true;

    public static bool Q_KeepLastGoodPreview = true;

    public static bool R_RenderTiming = true;

    public static bool S_LegacyPreview = true;

    public static bool T_PersistentSession = true;

    public static bool U_RecentFiles = true;

    public static bool V_WatchFolder = true;

    public static bool W_ArchiHintRoundTrip = true;

    public static bool X_DiagnosticsPanel = true;

    public static bool Y_Minimap = true;

    public static bool Z_CommandPalette = true;

    public static bool AA_SelectNextOccurrence = true;

    public static bool AB_Accessibility = true;

    public static bool AC_CoArchiDirtyBadge = true;

    public static bool AD_InsertFromArchi = true;
}
