using UnityEngine;
using UnityEngine.UI;

namespace SCZip.UI
{
    /// <summary>
    /// Serialized references to the uGUI hierarchy under UICanvas.
    /// </summary>
    public sealed class AppShellView : MonoBehaviour
    {
        [Header("Shell")]
        public GameObject drawerOverlay;
        public RectTransform drawer;
        public GameObject shell;
        public Text titleLabel;
        public Text subtitleLabel;
        public Text breadcrumb;
        public RectTransform fileListContent;
        public GameObject emptyLabel;
        public Text emptyLabelText;
        public GameObject errorLabel;
        public Text errorLabelText;
        public GameObject loadingLabel;
        public GameObject actionBar;
        public Button btnMenu;
        public Button btnBack;
        public Button btnOverflow;
        public Button btnBrowseFolder;
        public Button btnSelectAll;

        [Header("Drawer")]
        public Button navRecent;
        public Button navMyFiles;
        public Button navStorage;
        public Button navPhotos;
        public Button navMusic;
        public Button navSettings;
        public Button navExit;

        [Header("Action Bar")]
        public Button actUnzip;
        public Button actCompress;
        public Button actShare;
        public Button actDelete;
        public Button actMore;

        [Header("Settings")]
        public GameObject settingsPanel;
        public Button settingsBack;
        public Button setFormatZip;
        public Button setFormatTgz;
        public Button setLevelNormal;
        public Button setLevelMax;
        public Button setClearCache;

        [Header("Path Picker")]
        public GameObject pathPicker;
        public RectTransform pickerListContent;
        public RectTransform pickerQuickRoots;
        public Text pickerBreadcrumb;
        public Text pickerTitle;
        public Button pickerBack;
        public Button pickerConfirm;
        public Button pickerNativeBrowse;
        public Text pickerConfirmLabel;

        [Header("Dialog")]
        public GameObject dialogOverlay;
        public Text dialogTitle;
        public Text dialogMessage;
        public InputField dialogInput;
        public Dropdown dialogFormat;
        public ArchiveFormatSelector dialogFormatSelector;
        public Button dialogOk;
        public Button dialogCancel;

        [Header("Toast")]
        public GameObject toast;
        public Text toastLabel;
    }
}
