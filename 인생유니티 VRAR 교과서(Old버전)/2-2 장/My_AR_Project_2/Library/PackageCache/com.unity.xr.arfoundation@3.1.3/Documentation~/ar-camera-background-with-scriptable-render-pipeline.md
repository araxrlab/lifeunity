# Configuring the AR Camera background using a Scriptable Render Pipeline

AR Foundation supports the Universal Render Pipeline (URP) versions 7.0.0 or later. See the [URP Install and Configure documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest?subfolder=/manual/InstallingAndConfiguringURP.html) for more details on switching to URP.

**Note:** Projects made using URP are not compatible with the High Definition Render Pipeline or the built-in Unity rendering pipeline. Before you start development, you must decide which render pipeline to use in your Project.

## Basic URP configuration for AR Foundation

To configure URP for use with AR Foundation, follow these steps:

1. In the Project's `Assets` folder, create a new folder named `Rendering`.
   ![`Rendering` folder in the Project's `Assets` folder](images/srp/rendering-folder.png "Rendering Folder")
2. In the `Rendering` folder, create a Pipeline Asset (Forward Renderer) for URP:
    Right-click anywhere in the folder and select **Create &gt; Rendering &gt; Universal Render Pipeline &gt; Pipeline Asset (Forward Renderer)**.
    This creates two Assets:
    * An [UniversalRenderPipelineAsset](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest?subfolder=/api/UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.html)
    * A [ForwardRenderer](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest?subfolder=/api/UnityEngine.Rendering.Universal.ForwardRenderer.html)
   ![Creating a Pipeline Asset](images/srp/create-pipeline-asset.png "Create Pipeline Asset")
3. Select the `Forward Renderer`. In its Inspector, add an `ARBackgroundRendererFeature` to the list of Renderer Features.
   ![Adding an `ARBackgroundRendererFeature`](images/srp/add-renderer-feature.png "Adding an ARBackgroundRendererFeature")
4. Access the Graphics section of the Project Settings window (menu: **Edit &gt; Project Settings**, then select **Graphics**), and select the `UniversalRenderPipelineAsset` for the **Scriptable Render Pipeline Settings** field.
   ![Setting the Pipeline Asset](images/srp/set-pipeline-asset.png "Set Pipeline Asset")
