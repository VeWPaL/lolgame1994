# Img.cs - moved comments

Long comments moved out of `unity/Assets/Depths/Runtime/Art/Img.cs`. The code keeps a one-line gist tagged `[h:Img-N]`.

## [h:Img-1]
near: `public sealed class Img`

/// <summary>
/// A CPU image with the few canvas operations the JS art uses (fillRect, arc fill, drawImage,
/// pattern fill, radial gradient), composited source-over. Pure C#, so every bake is testable
/// without a GPU; textures are made from it once (<see cref="Atlas"/>). y points down, as in canvas.
/// </summary>
