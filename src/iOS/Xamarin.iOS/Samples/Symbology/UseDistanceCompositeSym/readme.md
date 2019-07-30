# Distance composite scene symbol

Change a graphic's symbol based on the camera's proximity to it.

![screenshot](UseDistanceCompositeSym.jpg)

## How to use the sample

The sample starts looking at a plane. Zoom out from the plane to see it turn into a cone. Keeping zooming out and it will turn into a point.

## How it works

1. Create a `GraphicsOverlay` object and add it to a `SceneView` object.
2. Create a `DistanceCompositeSceneSymbol` object.
3. Create `DistanceSymbolRange` objects specifying a `Symbol` and the min and max distance within which the symbol should be visible.
4. Add the ranges to the range collection of the distance composite scene symbol.
5. Create a `Graphic` object with the distance composite scene symbol at a location and add it to the graphics overlay.

## Relevant API

* DistanceCompositeSceneSymbol
* Range
* RangeCollection

## Tags

3D, DistanceCompositeSceneSymbol, DistanceSymbolRange, SimpleMarkerSceneSymbol, graphic