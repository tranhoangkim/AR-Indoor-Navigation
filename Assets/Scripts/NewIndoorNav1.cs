using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class NewIndoorNav_t : MonoBehaviour {
    public XROrigin XROrigin;
    public TMP_Dropdown endpointDropdown;
    public Button realignButton; // Assign this in the Inspector

    [SerializeField] private Transform player;
    [SerializeField] private ARTrackedImageManager m_TrackedImageManager;
    [SerializeField] private GameObject trackedImagePrefab;
    [SerializeField] private LineRenderer line;

    private List<NavigationTarget> navigationTargets = new List<NavigationTarget>();
    
    private NavMeshSurface navMeshSurface;
    private NavMeshPath navMeshPath;

    private GameObject navigationBase;

    private int selectedTargetIndex = -1; // Tracks the selected target
    private string alignedImageName = null; // Tracks the image we�re aligning with

    // --- NEW state for waypoint‐based routing ---
    [SerializeField] private float targetThreshold = 0.5f; 
    private List<VirtualTarget> virtualTargets         = new List<VirtualTarget>();
    private List<VirtualTarget> unvisitedVirtuals      = new List<VirtualTarget>();
    private NavigationTarget       finalDestination    = null;
    private Transform              currentDestination  = null;
    private bool                   isNavigating        = false;

    private void Start() {
        navMeshPath = new NavMeshPath();
        // disable screen dimming
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        endpointDropdown.gameObject.SetActive(false); // Hide dropdown initially

        // Add listener to the realign button
        realignButton.onClick.AddListener(RealignWithNewMarker);

    }

    private void Update() {
        if (navigationBase != null && selectedTargetIndex >= 0 && navMeshSurface != null) {
            //navMeshSurface.BuildNavMesh();

            if (!isNavigating || currentDestination == null) 
                return;

            Vector3 begin = player.position;
            Vector3 end = navigationTargets[selectedTargetIndex].transform.position;
            end.y = 0.1f;

            NavMesh.CalculatePath(begin, end, NavMesh.AllAreas, navMeshPath);

            if (navMeshPath.status == NavMeshPathStatus.PathComplete) {
                line.positionCount = navMeshPath.corners.Length;
                line.SetPositions(navMeshPath.corners);
            } else {
                line.positionCount = 0;
            }
        }
    }

    private void OnEnable() => m_TrackedImageManager.trackablesChanged.AddListener(OnChanged);

    private void OnDisable() => m_TrackedImageManager.trackablesChanged.RemoveListener(OnChanged);

    private void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs) {
        foreach (var newImage in eventArgs.added) {
            if (alignedImageName == null) // Align with new marker if not already aligned
            {
                alignedImageName = newImage.referenceImage.name;
                navigationBase = GameObject.Instantiate(trackedImagePrefab);

                navigationTargets.Clear();

                navigationTargets = navigationBase.transform.GetComponentsInChildren<NavigationTarget>().ToList();
                navMeshSurface = navigationBase.transform.GetComponentInChildren<NavMeshSurface>();

                AlignXROrigin(newImage);
                PopulateDropdown();
                endpointDropdown.gameObject.SetActive(true); // Show dropdown                
            }            

        }

        foreach (var updatedImage in eventArgs.updated)
        {
            if (updatedImage.referenceImage.name == alignedImageName)
            {
                navigationBase.transform.SetPositionAndRotation(updatedImage.pose.position, Quaternion.Euler(0, updatedImage.pose.rotation.eulerAngles.y, 0));
                // AlignXROrigin(updatedImage);
            }
        }

        foreach (var removedImagePair in eventArgs.removed)
        {
            // var removedImage = removedImagePair.Value; // Extract ARTrackedImage from KeyValuePair
            // if (removedImage.referenceImage.name == alignedImageName)
            // {
            //     alignedImageName = null;
            //     Destroy(navigationBase);
            //     navigationTargets.Clear();
            //     endpointDropdown.gameObject.SetActive(false);
            // }
        }
    }

    private void AlignXROrigin(ARTrackedImage trackedImage)
    {
        if (XROrigin == null)
        {
            Debug.LogError("XR Origin reference not set on NewIndoorNav script!");
            XROrigin = FindFirstObjectByType<XROrigin>();
            if (XROrigin == null)
            {
                Debug.LogError("Could not find XROrigin in the scene.");
                return;
            }
        }

        string imageName = trackedImage.referenceImage.name;
        ShowToast($"Detected {imageName}");
        NavigationTarget matchingTarget = navigationTargets.FirstOrDefault(t => t.gameObject.name == imageName);

        if (matchingTarget != null)
        {
            // XROrigin.transform.position = matchingTarget.transform.position;
            var t = matchingTarget.transform.position;
            XROrigin.transform.position = new Vector3(t.x, 0.0f, t.z);        
            ShowToast($"Aligned XR Origin with {matchingTarget.gameObject.name}");
        }
        else
        {
            XROrigin.transform.position = Vector3.zero;
            ShowToast($"XR Origin at (0,0,0)");
        }
    }

    private void PopulateDropdown()
    {
        endpointDropdown.ClearOptions();

        // Create the list of options with "No place chosen" as the first entry
        List<string> targetNames = new List<string> { "No place chosen" }; // Start with placeholder
        targetNames.AddRange(navigationTargets.Select(t => t.gameObject.name).ToList()); // Add actual targets

        endpointDropdown.AddOptions(targetNames);

        // Ensure the dropdown starts with "No place chosen" (index 0)
        endpointDropdown.value = 0; // Explicitly set to the placeholder
        endpointDropdown.RefreshShownValue(); // Update the UI to reflect the selection

        // Add the listener for value changes
        endpointDropdown.onValueChanged.AddListener(OnEndpointSelected);
    }

    private void OnEndpointSelected(int index)
    {
        // If the "No place chosen" option is selected (index 0), reset the target
        if (index == 0)
        {
            selectedTargetIndex = -1; // No target selected
            ShowToast("Please select a destination");
            endpointDropdown.captionText.text = "No place chosen";
            return;
        }

        // Adjust index to account for the placeholder option
        selectedTargetIndex = index - 1; // Shift index since "No place chosen" is at 0
        string selectedName = navigationTargets[selectedTargetIndex].gameObject.name;
        ShowToast($"Selected endpoint: {selectedName}");
        endpointDropdown.captionText.text = selectedName;
    }

    private void RealignWithNewMarker()
    {
        // Reset alignment state
        alignedImageName = null;
        if (navigationBase != null)
        {
            Destroy(navigationBase);
        }
        navigationTargets.Clear();
        endpointDropdown.gameObject.SetActive(false);
        selectedTargetIndex = -1;
        ShowToast("Ready to align with a new marker");

        // Clear the LineRenderer
        line.positionCount = 0; // Removes all points
        // line.enabled = false;  // Disables rendering

    }

    void ShowToast(string message)
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
                AndroidJavaClass unityPlayer = new("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaClass toastClass = new("android.widget.Toast");

                currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    toastClass.CallStatic<AndroidJavaObject>("makeText",
                                                            currentActivity,
                                                            message,
                                                            toastClass.GetStatic<int>("LENGTH_SHORT"))
                            .Call("show");
                }));
        #else
                Debug.Log(message);
        #endif
    }    
}