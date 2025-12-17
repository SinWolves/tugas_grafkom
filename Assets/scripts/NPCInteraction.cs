using UnityEngine;
using UnityEngine.UI;

public class NPCInteraction : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pressFPopup;
    public GameObject dialoguePanel;
    public GameObject giftSelectionPanel;

    [Header("Camera References")]
    public Camera mcCamera;
    public Camera npcCamera;

    [Header("Player References")]
    public PlayerMovement playerMovement;
    public PlayerGiftInventory playerInventory;
    public NPCAffinity npcAffinity;

    [Header("Gift Visuals")]
    public Transform giftSpawnPoint;
    public GameObject rosePrefab;
    public GameObject chocolatePrefab;
    public GameObject sundaePrefab;

    [Header("UI Text Elements")]
    public Text roseTextUI;
    public Text chocolateTextUI;
    public Text sundaeTextUI;

    [Header("NPC Appearance Settings - PENTING!")]
    public bool appearsInMorning = false;
    public bool appearsInNoon = false;
    public bool appearsInEvening = false;
    public bool appearsInNight = false;

    [Header("Settings")]
    public float cameraTransitionSpeed = 5f;

    // Private variables
    private bool playerNearby;
    private bool inDialogue;
    private bool transitioning;
    private bool givingGift;
    private bool isNPCEnabled = false; // Default false

    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;
    private Vector3 transitionTargetPos;
    private Quaternion transitionTargetRot;
    private float transitionProgress;

    private Renderer[] playerRenderers;
    private Vector3 npcCameraLocalPos;
    private Quaternion npcCameraLocalRot;

    private float giftTimer;
    private GameObject currentGift;

    // Reference ke DayCycleManager
    private DayCycleManager dayCycle;
    private Collider npcCollider;
    private CharacterAppear characterAppear;

    void Awake()
    {
        // Dapatkan semua komponen
        npcCollider = GetComponent<Collider>();
        if (npcCollider == null)
            npcCollider = GetComponentInChildren<Collider>();

        characterAppear = GetComponent<CharacterAppear>();
        if (characterAppear == null)
            characterAppear = GetComponentInChildren<CharacterAppear>();

        if (npcCamera != null)
        {
            npcCameraLocalPos = npcCamera.transform.localPosition;
            npcCameraLocalRot = npcCamera.transform.localRotation;
            npcCamera.gameObject.SetActive(false);
        }
    }

    void Start()
    {

        // Dapatkan DayCycleManager
        dayCycle = FindObjectOfType<DayCycleManager>();
        if (dayCycle != null)
        {
            dayCycle.OnTimeChanged += OnTimeChanged;
            // Cek status awal - PASTIKAN ini dipanggil
            UpdateNPCState(dayCycle.currentTime, true);
        }
        else
        {
            Debug.LogError("DayCycleManager not found!");
        }

        // PASTIKAN semua UI nonaktif di awal
        ForceDisableAllUI();

        // Reset state
        playerNearby = false;
        inDialogue = false;
        transitioning = false;
        givingGift = false;

        // Pastikan mcCamera aktif
        if (mcCamera != null)
        {
            mcCamera.gameObject.SetActive(true);
        }

        if (playerMovement != null)
        {
            playerRenderers = playerMovement.GetComponentsInChildren<Renderer>();
            playerMovement.canMove = true;
        }

    }

    void ForceDisableAllUI()
    {
        if (pressFPopup != null)
        {
            pressFPopup.SetActive(false);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        if (giftSelectionPanel != null)
        {
            giftSelectionPanel.SetActive(false);
        }
    }

    // Dipanggil saat waktu berubah
    void OnTimeChanged(DayCycleManager.TimeOfDay time)
    {
        UpdateNPCState(time, false);
    }

    void UpdateNPCState(DayCycleManager.TimeOfDay currentTime, bool isStart = false)
    {
        // DEBUG DETAILED

        bool shouldBeEnabled = currentTime switch
        {
            DayCycleManager.TimeOfDay.Morning => appearsInMorning,
            DayCycleManager.TimeOfDay.Noon => appearsInNoon,
            DayCycleManager.TimeOfDay.Evening => appearsInEvening,
            DayCycleManager.TimeOfDay.Night => appearsInNight,
            _ => false
        };


        // Jika status berubah ATAU ini adalah start pertama
        if (isNPCEnabled != shouldBeEnabled || isStart)
        {
            bool wasEnabled = isNPCEnabled;
            isNPCEnabled = shouldBeEnabled;

            // Kontrol collider
            if (npcCollider != null)
            {
                npcCollider.enabled = shouldBeEnabled;
            }

            // Jika NPC dinonaktifkan
            if (!shouldBeEnabled)
            {
                // Force reset semua state
                playerNearby = false;

                // Nonaktifkan SEMUA UI
                ForceDisableAllUI();

                // Jika sedang dalam dialog, paksa keluar
                if (inDialogue || transitioning || givingGift)
                {
                    ForceEndDialogue();
                }

                // Nonaktifkan gameobject jika ada CharacterAppear
                if (characterAppear != null && !characterAppear.enabled)
                {
                    gameObject.SetActive(false);
                }
            }
            else // Jika NPC diaktifkan
            {
                // Aktifkan gameobject jika perlu
                if (characterAppear != null)
                {
                    gameObject.SetActive(true);
                }
            }
        }
    }

    void ForceEndDialogue()
    {
        // Reset semua state
        inDialogue = false;
        givingGift = false;
        transitioning = false;
        playerNearby = false;

        // Nonaktifkan semua UI
        ForceDisableAllUI();

        // Kamera
        if (npcCamera != null && npcCamera.gameObject.activeSelf)
        {
            npcCamera.gameObject.SetActive(false);
        }

        if (mcCamera != null && !mcCamera.gameObject.activeSelf)
        {
            mcCamera.gameObject.SetActive(true);
        }

        // Player
        if (playerMovement != null)
        {
            playerMovement.canMove = true;
            SetPlayerVisible(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isNPCEnabled)
        {
            // Safety: pastikan UI nonaktif
            if ((pressFPopup != null && pressFPopup.activeSelf) ||
                (dialoguePanel != null && dialoguePanel.activeSelf) ||
                (giftSelectionPanel != null && giftSelectionPanel.activeSelf))
            {
                ForceDisableAllUI();
            }
            return;
        }

        // Safety check: jika kamera NPC aktif tapi tidak dalam dialog
        if (!inDialogue && !transitioning)
        {
            if (npcCamera != null && npcCamera.gameObject.activeSelf)
            {
                npcCamera.gameObject.SetActive(false);
            }

            if (mcCamera != null && !mcCamera.gameObject.activeSelf)
            {
                mcCamera.gameObject.SetActive(true);
            }
        }

        // Input untuk mulai dialog
        if (playerNearby && !inDialogue && Input.GetKeyDown(KeyCode.F))
        {
            StartDialogue();
        }

        // Input dalam dialog
        if (inDialogue && !transitioning && !givingGift)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                OnLeave();
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                OnGiveGift();
            }
        }

        // Camera transition
        if (transitioning)
        {
            CameraTransition();
        }

        // Gift sequence
        if (givingGift)
        {
            HandleGiftSequence();
        }

        // Gift selection input
        if (giftSelectionPanel != null && giftSelectionPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                TryGiveGift(GiftType.Rose);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                TryGiveGift(GiftType.Chocolate);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                TryGiveGift(GiftType.Sundae);
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                giftSelectionPanel.SetActive(false);
                if (dialoguePanel != null)
                    dialoguePanel.SetActive(true);
            }
        }
    }

    public void OnGiveGift()
    {
        if (!isNPCEnabled) return;

        if (givingGift) return;

        if (giftSelectionPanel != null)
        {
            giftSelectionPanel.SetActive(true);
            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);
            UpdateGiftUI();
        }
        else
        {
            Debug.LogError("giftSelectionPanel belum diassign!");
        }
    }

    void UpdateGiftUI()
    {
        if (playerInventory == null) return;

        if (roseTextUI != null)
            roseTextUI.text = "Mawar (You have " + playerInventory.roseCount + ")";

        if (chocolateTextUI != null)
            chocolateTextUI.text = "Coklat (You have " + playerInventory.chocolateCount + ")";

        if (sundaeTextUI != null)
            sundaeTextUI.text = "Sundae (You have " + playerInventory.sundaeCount + ")";
    }

    void TryGiveGift(GiftType type)
    {
        if (playerInventory == null || npcAffinity == null)
        {
            Debug.LogError("PlayerInventory atau NPCAffinity belum diassign!");
            return;
        }

        if (!playerInventory.HasGift(type))
        {
            Debug.Log("You don't have this gift!");
            return;
        }

        // Consume gift
        playerInventory.ConsumeGift(type);
        UpdateGiftUI();

        // Calculate points
        int points = type switch
        {
            GiftType.Rose => 10,
            GiftType.Chocolate => 50,
            GiftType.Sundae => 100,
            _ => 0
        };

        // Add affinity points
        npcAffinity.AddPoints(points);

        // Start gift giving sequence
        StartGiftSequence(type);
    }

    void StartGiftSequence(GiftType type)
{
    if (giftSelectionPanel != null)
        giftSelectionPanel.SetActive(false);

    givingGift = true;
    giftTimer = 0f;

    GameObject prefab = type switch
    {
        GiftType.Rose => rosePrefab,
        GiftType.Chocolate => chocolatePrefab,
        GiftType.Sundae => sundaePrefab,
        _ => null
    };

    if (prefab == null || giftSpawnPoint == null)
        return;

    currentGift = Instantiate(prefab);

    // PENTING: set parent dulu
    currentGift.transform.SetParent(giftSpawnPoint, false);

    // reset transform
    currentGift.transform.localPosition = Vector3.zero;
    currentGift.transform.localRotation = Quaternion.identity;

    // KECILKAN DI SINI
    currentGift.transform.localScale = Vector3.one * 0.05f;
}


    void HandleGiftSequence()
    {
        giftTimer += Time.deltaTime;

        // Phase 1: NPC looks down (0-0.5 seconds)
        if (giftTimer < 0.5f)
        {
            if (npcCamera != null)
            {
                Vector3 rot = npcCamera.transform.rotation.eulerAngles;
                rot.x += 20f * Time.deltaTime; // Look down gradually
                npcCamera.transform.rotation = Quaternion.Euler(rot);
            }
        }
        // Phase 2: Wait with gift (0.5-2 seconds)
        else if (giftTimer < 2f)
        {
            // Gift stays visible
        }
        // Phase 3: Finish sequence (>2 seconds)
        else
        {
            // Clean up gift
            if (currentGift != null)
                Destroy(currentGift);

            // Reset NPC rotation
            if (npcCamera != null)
            {
                // Reset ke posisi awal kamera NPC
                Transform npcParent = npcCamera.transform.parent;
                if (npcParent != null)
                {
                    Vector3 targetPos = npcParent.TransformPoint(npcCameraLocalPos);
                    Quaternion targetRot = npcParent.rotation * npcCameraLocalRot;
                    npcCamera.transform.position = targetPos;
                    npcCamera.transform.rotation = targetRot;
                }
            }

            givingGift = false;
            Debug.Log("NPC: Thank you for the gift!");

            // Kembali ke dialog atau end dialogue
            OnLeave();
        }
    }

    // ===== FUNGSI DIALOG & KAMERA (TETAP SAMA) =====

    void StartDialogue()
    {
        if (!isNPCEnabled) return;

        inDialogue = true;
        if (pressFPopup != null)
            pressFPopup.SetActive(false);

        if (playerMovement != null)
        {
            playerMovement.canMove = false;
        }

        SetPlayerVisible(false);

        // Camera transition setup
        if (npcCamera != null && mcCamera != null)
        {
            Transform npcParent = npcCamera.transform.parent;
            if (npcParent != null)
            {
                transitionTargetPos = npcParent.TransformPoint(npcCameraLocalPos);
                transitionTargetRot = npcParent.rotation * npcCameraLocalRot;
            }
            else
            {
                transitionTargetPos = npcCameraLocalPos;
                transitionTargetRot = npcCameraLocalRot;
            }

            transitionStartPos = mcCamera.transform.position;
            transitionStartRot = mcCamera.transform.rotation;

            npcCamera.transform.position = transitionStartPos;
            npcCamera.transform.rotation = transitionStartRot;
            npcCamera.gameObject.SetActive(true);
            mcCamera.gameObject.SetActive(false);

            transitionProgress = 0f;
            transitioning = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void EndDialogue()
    {
        if (!isNPCEnabled) return;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (giftSelectionPanel != null) giftSelectionPanel.SetActive(false);

        // Camera transition back
        if (npcCamera != null && mcCamera != null)
        {
            transitionStartPos = npcCamera.transform.position;
            transitionStartRot = npcCamera.transform.rotation;
            transitionTargetPos = mcCamera.transform.position;
            transitionTargetRot = mcCamera.transform.rotation;

            transitionProgress = 0f;
            transitioning = true;
        }

        if (playerMovement != null)
        {
            playerMovement.canMove = true;
        }

        SetPlayerVisible(true);

        inDialogue = false;
        givingGift = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void CameraTransition()
    {
        if (npcCamera == null || mcCamera == null) return;

        transitionProgress += Time.deltaTime * cameraTransitionSpeed;

        npcCamera.transform.position = Vector3.Lerp(transitionStartPos, transitionTargetPos, transitionProgress);
        npcCamera.transform.rotation = Quaternion.Slerp(transitionStartRot, transitionTargetRot, transitionProgress);

        if (transitionProgress >= 1f)
        {
            transitioning = false;

            if (inDialogue)
            {
                if (dialoguePanel != null)
                    dialoguePanel.SetActive(true);
            }
            else
            {
                npcCamera.gameObject.SetActive(false);
                mcCamera.gameObject.SetActive(true);
            }
        }
    }

    void SetPlayerVisible(bool visible)
    {
        if (playerRenderers == null) return;

        foreach (Renderer r in playerRenderers)
            r.enabled = visible;
    }

    void OnTriggerEnter(Collider other)
    {

        // HANYA respon jika NPC enabled
        if (!isNPCEnabled)
        {
            Debug.Log($"Ignoring trigger - NPC disabled");
            return;
        }

        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            if (pressFPopup != null)
            {
                pressFPopup.SetActive(true);
                Debug.Log($"Showing pressFPopup for {gameObject.name}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {

        if (!isNPCEnabled) return;

        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            if (pressFPopup != null)
            {
                pressFPopup.SetActive(false);
                Debug.Log($"Hiding pressFPopup for {gameObject.name}");
            }
        }
    }

    void OnDestroy()
    {
        if (dayCycle != null)
        {
            dayCycle.OnTimeChanged -= OnTimeChanged;
        }
    }

    public void OnLeave()
    {
        if (!isNPCEnabled) return;
        EndDialogue();
    }
}