# Migration guide

This guide covers the differences between AR Foundation 2.x and 3.x.

## Image tracking

The image tracking manager `ARTrackedImageManager` has a `referenceLibrary` property on it to set the reference image library (the set of images to detect in the environment). Previously, this was an `XRReferenceImageLibrary`. Now, it is an `IReferenceImageLibrary`, and `XRReferenceImageLibrary` implements `IReferenceImageLibrary`. If your code previously set the the `referenceLibrary` property to an `XRReferenceImageLibrary`, it should continue to work as before. However, if you previously treated the `referenceLibrary` as an `XRReferenceImageLibrary`, you will have to attempt to cast it to a `XRReferenceImageLibrary`.

In the Editor, this will always be an `XRReferenceImageLibrary`. However, at runtime with image tracking enabled, `ARTrackedImageManager.referenceLibrary` will return a new type, `RuntimeReferenceImageLibrary`. This still behaves like an `XRReferenceImageLibrary` (for instance, you can enumerate its reference images), and it might also have additional functionality (see `MutableRuntimeReferenceImageLibrary`).

## Background shaders

The `ARCameraBackground` has been updated to support the [Universal Render Pipeline (URP)](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest) when that package is present. This involved a breaking change to the `XRCameraSubsystem`: the property `shaderName` is now `cameraMaterial`. Most developers didn't need to access this directly because the shader name was only used by AR Foundation to construct the background material. That functionality has now moved to the `XRCameraSubsystem`.

## Point clouds

The [`ARPointCloud`](point-cloud-manager.md) properties
[`positions`](../api/UnityEngine.XR.ARFoundation.ARPointCloud.html#UnityEngine_XR_ARFoundation_ARPointCloud_positions),
[`confidenceValues`](../api/UnityEngine.XR.ARFoundation.ARPointCloud.html#UnityEngine_XR_ARFoundation_ARPointCloud_confidenceValues),
and
[`identifiers`](../api/UnityEngine.XR.ARFoundation.ARPointCloud.html#UnityEngine_XR_ARFoundation_ARPointCloud_identifiers)
have changed from returning [`NativeArray`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html)s to [nullabe](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/nullable-types/) [`NativeSlice`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeSlice_1.html)s. The `ARPointCloud` manages the memory contained in these `NativeArray`s, so callers should only be able to see a `NativeSlice` (that is, you should not be able to [`Dispose`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.Dispose.html) of the `NativeArray`).

Additionally, these arrays aren't necessarily present. Previously, you could check for their existence with [`NativeArray<T>.IsCreated`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.IsCreated.html). `NativeSlice` doesn't have an `IsCreated` property, so these properties have been made nullable.

## Face tracking

The [`ARFaceManager`](face-manager.md)'s `supported` property has been removed. If face tracking is not supported, the manager's subsystem is null. This was done for consistency as no other manager has this property. If a manager's subsystem is null after enabling the manager, that generally means the subsystem is not supported.

## Reference Points renamed to Anchors

To align with industry standard terminology, we've renamed "Reference Points" to "Anchors":

| **Old class** | **New class** |
| --------- | --------- |
| `ARReferencePointManager` | `ARAnchorManager` |
| `ARReferencePoint` | `ARAnchor` |

When you open an existing Project that used the old reference point API, Unity will prompt you with the option to automatically update your scripts to the new API.
