using UnityEngine;

public class VoxelTrigger : MonoBehaviour
{
    [HideInInspector] public VoxelCube owner;
    [HideInInspector] public int voxelIndex;

    private void OnTriggerEnter(Collider other)
    {
        // 只對帶有 Rigidbody 的動態物件生效
        if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
        {
            owner.RemoveVoxelByIndex(voxelIndex);
            // 切完就刪掉自己，避免重複觸發
            Destroy(gameObject);
        }
    }
}
