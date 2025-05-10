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

public class NewIndoorNav1 : MonoBehaviour {
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
    [SerializeField] private float targetThreshold;
    private List<VirtualTarget> virtualTargets         = new List<VirtualTarget>();
    private List<VirtualTarget> unvisitedVirtuals      = new List<VirtualTarget>();
    private NavigationTarget       finalDestination    = null;
    private Transform              currentDestination  = null;
    private bool                   isNavigating        = false;

    [SerializeField] private float toastInterval = 1f;
    private float lastToastTime = 0f;
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
            Vector3 end = currentDestination.position; 
            NavMesh.CalculatePath(begin, end, NavMesh.AllAreas, navMeshPath);
            if (navMeshPath.status == NavMeshPathStatus.PathComplete) {
                line.positionCount = navMeshPath.corners.Length;
                line.SetPositions(navMeshPath.corners);
            } else {
                line.positionCount = 0;
            }

            float now = Time.time;
            // compute the distance once
            float dist = Vector3.Distance(player.position, currentDestination.position);

            // evaluate the comparison
            bool isWithin = dist < targetThreshold;
            // if (now - lastToastTime > toastInterval) {
            //     // show both the distance and the comparison result
            //     ShowToast($"Dist: {dist:F2}m");
            //     lastToastTime = now;
            // }

            // Check if we've arrived at the *current* target
            if (isWithin) {
                HandleArrivalAtCurrent();
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
                virtualTargets = navigationBase.transform.GetComponentsInChildren<VirtualTarget>().ToList();
                unvisitedVirtuals = new List<VirtualTarget>(virtualTargets);

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

    private void OnEndpointSelected(int index) {
        if (index == 0) {
            isNavigating = false;
            ShowToast("Please select a destination");
            endpointDropdown.captionText.text = "No place chosen";
            return;
        }

        // set up final destination
        selectedTargetIndex = index - 1;
        finalDestination = navigationTargets[selectedTargetIndex];
        ShowToast($"Final destination: {finalDestination.gameObject.name}");

        // reset waypoint state
        unvisitedVirtuals = new List<VirtualTarget>(virtualTargets);
        // compute first *current* destination
        ComputeNextDestination();
        isNavigating = true;
    }

    private void ComputeNextDestination() {
        var pos = player.position;
        float dFinal = Vector3.Distance(pos, finalDestination.transform.position);

        // find nearest unvisited virtual target
        VirtualTarget nearest = null;
        float        dVirt   = float.MaxValue;
        foreach (var vt in unvisitedVirtuals) {
            float d = Vector3.Distance(pos, vt.transform.position);
            if (d < dVirt) {
                dVirt = d;
                nearest = vt;
            }
        }

        // pick whichever is closer: waypoint or final
        if (nearest != null && dVirt < dFinal) {
            currentDestination = nearest.transform;
            ShowToast($"Next waypoint: {nearest.gameObject.name}");
        } else {
            currentDestination = finalDestination.transform;
            ShowToast($"Heading for destination: {finalDestination.gameObject.name}");
        }
    }

    private void HandleArrivalAtCurrent() {
        // If we've reached the true final destination:
        if (currentDestination == finalDestination.transform) {
            ShowToast("🎉 Arrived at destination!");
            isNavigating = false;
            return;
        }

        // Otherwise, we've reached a VirtualTarget—mark it visited:
        var arrived = unvisitedVirtuals
            .FirstOrDefault(v => v.transform == currentDestination);
        if (arrived != null) {
            unvisitedVirtuals.Remove(arrived);
            // optional: hide it so you can't revisit
            // arrived.gameObject.SetActive(false);
            ShowToast($"Reached waypoint {arrived.gameObject.name}");
        }

        // pick the next one
        ComputeNextDestination();
    }

    private void RealignWithNewMarker() {
        // your existing cleanup...
        alignedImageName = null;
        if (navigationBase != null) Destroy(navigationBase);
        navigationTargets.Clear();
        endpointDropdown.gameObject.SetActive(false);
        selectedTargetIndex = -1;

        // reset navigation state
        isNavigating = false;
        line.positionCount = 0;

        ShowToast("Ready to align with a new marker");
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