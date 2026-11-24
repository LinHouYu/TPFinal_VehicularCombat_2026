using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VehicularCombat.Vehicle
{
    /// <summary>
    /// Unity 6 official WheelCollider vehicle controller adapted for Input System,
    /// horizontal recoil impulses (ApplyRecoil), and rear-wheel oversteer Drift Tires buff
    /// with TrailRenderer visual feedback.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarControl : MonoBehaviour
    {
        [Header("Car Properties")]
        public float motorTorque = 900f;
        public float brakeTorque = 1800f;
        public float maxSpeed = 18f;
        public float steeringRange = 32f;
        public float steeringRangeAtMaxSpeed = 14f;
        public float centreOfGravityOffset = -1f;

        [Header("Handling")]
        [Tooltip("Keep 0 when coasting so PhysX WheelCollider sticky-brake never swallows weapon recoil impulses.")]
        public float idleBrakeTorque = 0f;

        [Header("Drift Tires Buff")]
        [Tooltip("Rear-wheel-only sideways stiffness during drift buff (front wheels keep full grip for snappy oversteer).")]
        public float driftRearSidewaysStiffness = 0.22f;
        public float driftMotorMultiplier = 1.5f;
        public float driftMaxSpeedMultiplier = 1.4f;
        public TrailRenderer[] driftTrails;

        // Public HUD-ready state properties
        public bool IsDriftTiresActive { get; private set; }
        public float DriftTimeRemaining { get; private set; }
        public float DriftDurationTotal { get; private set; }

        private WheelControl[] wheels;
        private Rigidbody rigidBody;
        private float baseMotorTorque;
        private float baseMaxSpeed;
        private float[] baseSidewaysStiffness;
        private Coroutine driftRoutine;

        public Rigidbody RigidBody => rigidBody;

        private void Awake()
        {
            rigidBody = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<WheelControl>();

            baseMotorTorque = motorTorque;
            baseMaxSpeed = maxSpeed;
            baseSidewaysStiffness = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null)
                {
                    var wc = wheels[i].GetComponent<WheelCollider>();
                    baseSidewaysStiffness[i] = wc != null ? wc.sidewaysFriction.stiffness : 1.25f;
                }
            }

            if (driftTrails == null || driftTrails.Length == 0)
                driftTrails = GetComponentsInChildren<TrailRenderer>(true);

            SetDriftTrailsEmitting(false);
        }

        private void Start()
        {
            Vector3 centerOfMass = rigidBody.centerOfMass;
            centerOfMass.y += centreOfGravityOffset;
            rigidBody.centerOfMass = centerOfMass;
        }

        private void OnDisable()
        {
            if (IsDriftTiresActive)
            {
                RestoreBasePhysics();
            }
        }

        private void FixedUpdate()
        {
            ReadDriveInput(out float vInput, out float hInput);

            float forwardSpeed = Vector3.Dot(transform.forward, rigidBody.linearVelocity);
            float speedFactor = Mathf.InverseLerp(0f, maxSpeed, Mathf.Abs(forwardSpeed));

            float currentMotorTorque = Mathf.Lerp(motorTorque, 0f, speedFactor);
            float currentSteerRange = Mathf.Lerp(steeringRange, steeringRangeAtMaxSpeed, speedFactor);

            bool hasThrottle = Mathf.Abs(vInput) > 0.01f;
            bool isAccelerating = Mathf.Abs(forwardSpeed) < 0.4f || Mathf.Sign(vInput) == Mathf.Sign(forwardSpeed);

            foreach (var wheel in wheels)
            {
                if (wheel == null || wheel.WheelCollider == null)
                    continue;

                if (wheel.steerable)
                {
                    wheel.WheelCollider.steerAngle = hInput * currentSteerRange;
                }

                if (!hasThrottle)
                {
                    wheel.WheelCollider.motorTorque = 0f;
                    wheel.WheelCollider.brakeTorque = idleBrakeTorque;
                }
                else if (isAccelerating)
                {
                    if (wheel.motorized)
                    {
                        wheel.WheelCollider.motorTorque = vInput * currentMotorTorque;
                    }
                    wheel.WheelCollider.brakeTorque = 0f;
                }
                else
                {
                    wheel.WheelCollider.motorTorque = 0f;
                    wheel.WheelCollider.brakeTorque = Mathf.Abs(vInput) * brakeTorque;
                }
            }
        }

        /// <summary>
        /// Applies horizontal firing recoil impulse opposite to shootDir (Y forced to 0)
        /// for gun-boost drifting, braking, and recoil-push gameplay.
        /// </summary>
        public void ApplyRecoil(Vector3 shootDir, float force)
        {
            if (rigidBody == null)
                return;

            shootDir.y = 0f;
            if (shootDir.sqrMagnitude < 0.0001f)
                return;

            if (wheels != null)
            {
                foreach (var wheel in wheels)
                {
                    if (wheel != null && wheel.WheelCollider != null)
                        wheel.WheelCollider.brakeTorque = 0f;
                }
            }

            rigidBody.AddForce(-shootDir.normalized * force, ForceMode.Impulse);
        }

        /// <summary>
        /// Activates timed Drift Tires buff: boosts maxSpeed & motorTorque, lowers ONLY rear wheels'
        /// sidewaysFriction.stiffness for snappy oversteer drifting, and enables rear TrailRenderers.
        /// Re-picking refreshes remaining time without stacking multipliers.
        /// </summary>
        public void ActivateDriftTires(float duration)
        {
            if (duration <= 0f)
                return;

            DriftDurationTotal = duration;
            DriftTimeRemaining = duration;

            if (driftRoutine != null)
            {
                ApplyDriftPhysics();
                SetDriftTrailsEmitting(true);
                Debug.Log($"[CarControl] Drift Tires Activated! (Timer refreshed -> {DriftTimeRemaining:F1}s)");
                return;
            }

            driftRoutine = StartCoroutine(DriftTiresCoroutine());
        }

        private IEnumerator DriftTiresCoroutine()
        {
            IsDriftTiresActive = true;
            ApplyDriftPhysics();
            SetDriftTrailsEmitting(true);
            Debug.Log($"[CarControl] Drift Tires Activated! duration={DriftTimeRemaining:F1}s, rearStiffness={driftRearSidewaysStiffness:F2}, motorTorque={motorTorque:F0}, maxSpeed={maxSpeed:F1}");

            while (DriftTimeRemaining > 0f)
            {
                yield return null;
                DriftTimeRemaining = Mathf.Max(0f, DriftTimeRemaining - Time.deltaTime);
            }

            RestoreBasePhysics();
            driftRoutine = null;
            Debug.Log($"[CarControl] Drift Tires Expired -> restored motorTorque={motorTorque:F0}, maxSpeed={maxSpeed:F1}");
        }

        private void ApplyDriftPhysics()
        {
            motorTorque = baseMotorTorque * driftMotorMultiplier;
            maxSpeed = baseMaxSpeed * driftMaxSpeedMultiplier;
            if (wheels == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel == null || wheel.steerable)
                    continue; // Keep front steering wheels grippy so rear tail kicks out cleanly!

                var wc = wheel.WheelCollider != null ? wheel.WheelCollider : wheel.GetComponent<WheelCollider>();
                if (wc == null) continue;

                WheelFrictionCurve friction = wc.sidewaysFriction;
                friction.stiffness = driftRearSidewaysStiffness;
                wc.sidewaysFriction = friction;
            }
        }

        private void RestoreBasePhysics()
        {
            motorTorque = baseMotorTorque;
            maxSpeed = baseMaxSpeed;
            if (wheels != null && baseSidewaysStiffness != null)
            {
                for (int i = 0; i < wheels.Length; i++)
                {
                    var wheel = wheels[i];
                    if (wheel == null) continue;

                    var wc = wheel.WheelCollider != null ? wheel.WheelCollider : wheel.GetComponent<WheelCollider>();
                    if (wc == null) continue;

                    WheelFrictionCurve friction = wc.sidewaysFriction;
                    friction.stiffness = baseSidewaysStiffness[i];
                    wc.sidewaysFriction = friction;
                }
            }

            SetDriftTrailsEmitting(false);
            IsDriftTiresActive = false;
            DriftTimeRemaining = 0f;
            DriftDurationTotal = 0f;
        }

        private void SetDriftTrailsEmitting(bool emitting)
        {
            if (driftTrails == null) return;
            foreach (var tr in driftTrails)
            {
                if (tr != null)
                    tr.emitting = emitting;
            }
        }

        private static void ReadDriveInput(out float vertical, out float horizontal)
        {
            vertical = 0f;
            horizontal = 0f;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) vertical += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) vertical -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
        }
    }
}
