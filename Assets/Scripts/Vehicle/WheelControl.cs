using UnityEngine;

namespace VehicularCombat.Vehicle
{
    /// <summary>
    /// Unity 6 official WheelCollider structure: syncs visual wheel mesh Transform
    /// from WheelCollider.GetWorldPose every frame.
    /// </summary>
    [RequireComponent(typeof(WheelCollider))]
    public class WheelControl : MonoBehaviour
    {
        public Transform wheelModel;

        [HideInInspector] public WheelCollider WheelCollider;

        // Create properties for the CarControl script
        public bool steerable;
        public bool motorized;

        private Vector3 position;
        private Quaternion rotation;

        private void Awake()
        {
            WheelCollider = GetComponent<WheelCollider>();
        }

        private void Update()
        {
            if (WheelCollider == null || wheelModel == null)
                return;

            // Get the WheelCollider's world pose values and apply them to the wheel model
            WheelCollider.GetWorldPose(out position, out rotation);
            wheelModel.position = position;
            wheelModel.rotation = rotation;
        }
    }
}
