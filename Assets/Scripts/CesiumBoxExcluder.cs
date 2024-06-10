using CesiumForUnity;
using UnityEngine;

public class CesiumBoxExcluder : CesiumTileExcluder
{
    private BoxCollider _boxCollider;
    private Bounds _bounds;

    public bool invert = false;

    protected void OnEnable()
    {
        this._boxCollider = this.gameObject.GetComponent<BoxCollider>();
        this._bounds = new Bounds(this._boxCollider.center, this._boxCollider.size);
    }

    protected void Update()
    {
        this._bounds.center = this._boxCollider.center;
        this._bounds.size = this._boxCollider.size;
    }

    public bool CompletelyContains(Bounds bounds)
    {
        return Vector3.Min(this._bounds.max, bounds.max) == bounds.max &&
               Vector3.Max(this._bounds.min, bounds.min) == bounds.min;
    }

    public override bool ShouldExclude(Cesium3DTile tile)
    {
        if (!this.enabled)
        {
            return false;
        }

        if (this.invert)
        {
            return this.CompletelyContains(tile.bounds);
        }

        return !this._bounds.Intersects(tile.bounds);
    }
}