# AR Indoor Navigation app

## Repository

First, clone this repository: 

```
git clone https://github.com/tranhoangkim/AR-Indoor-Navigation.git
```

## Environment Information

This repository was tested with Unity 6000.0.45f1.

This app is currently deployed on Android.

## Deployment process

1. In the menu bar of Unity, go to File -> Build profiles 
2. Switch to the Android platform
3. Ensure turn on the developer mode and debugging mode on your Android phone.
4. Connect your phone to your computer by cable.
5. In Platform Settings, go to Run Device and select your phone. 

## Main components

- The 3D mesh of the second floor of Jbhunt building: Assets -> Art -> Models -> floor2.glb

- Main scene in Unity: Go to Assets -> Scenes -> SampleScene.unity

- Scripts folder (Assets -> Scripts):
    + Assets -> Scripts -> NewIndoorNav.cs: all main code is located in this script.
    + NavigationTarget.cs: represents for each final destination.
    + VirtualTarget.cs: represents for each waypoint at the intersection of the second floor. 

- Assets -> Prefabs -> TrackedImage_fullfloor_main.prefab: this prefab contains 3D mesh which would be baked by NavMesh to compute optimal paths, final targets, waypoints. 

- The NavigationLine in SampleScene (go to Hierachy) represents for the navigation line which is drawn by direction arrows.

- Another AR components are very familiar in an AR app such as XR origin, AR Session.


## Sample code (in NewIndoorNav.cs)
1. Compute a navigation path


```
Vector3 begin = player.position; // Current position of the user
Vector3 end = currentDestination.position; // the destination

// computer the navigation path
NavMesh.CalculatePath(begin, end, NavMesh.AllAreas, navMeshPath); 

// Draw the navigation line
if (navMeshPath.status == NavMeshPathStatus.PathComplete) {
    line.positionCount = navMeshPath.corners.Length;
    line.SetPositions(navMeshPath.corners);
} else {
    line.positionCount = 0;
}
```

2. Hitting wall avoidance algorithm
Idea: Choose the nearest waypoint (at three-way or four-way junctions). The app always guides the user to walk straight to the nearest intersection first, and only then instructs them to turn left or right.

```
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
```

3. Show the drop-down menu that is used to select one of five destinations
```
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
```
