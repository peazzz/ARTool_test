using UnityEngine;

public class VoxelTrigger : MonoBehaviour
{
    [HideInInspector] public VoxelCube owner;
    [HideInInspector] public int voxelIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
        {
            owner.RemoveVoxelByIndex(voxelIndex);
            Destroy(gameObject);
        }
    }
}
