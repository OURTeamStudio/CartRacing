﻿using System.Collections.Generic;
using System.Text;
using Models.CarModule;
using ServiceLocatorModule;
using Services.Input;
using TMPro;
using UnityEngine;

namespace CarModule.CarControl
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        // debug
        [SerializeField] private TextMeshProUGUI infoText;

        // car config
        [SerializeField] private List<CarAxle> axles;
        [SerializeField] private Rigidbody carRigidbody;
        [SerializeField] private Transform centerOfMass;

        private CarConfig _config;

        // calculates
        private float _currentMotorTorque;
        private float _currentSpeed;
        private float _currentSteeringAngle;
        private bool _carIsGrounded;
        private CarMovingData _movingData;
        public CarMovingData MovingData => _movingData;

        // input
        private bool _handbrake;
        private float _brakeInput;
        private float _gasInput;
        private float _turnInput;

        // additional services
        private InputService _input;

        public void Initialize(CarConfig config)
        {
            _config = config;
            SaveMovingData();
        }

        private void Start()
        {
            _input = ServiceLocator.Instance.GetService<InputService>();

            carRigidbody.centerOfMass = centerOfMass.localPosition;
        }

        private void Update()
        {
            _currentSpeed = carRigidbody.velocity.magnitude;
            CheckInput();
            UpdateWheelMesh();
            SaveMovingData();
            ShowNewInfo();
            
        }


        private void FixedUpdate()
        {
            ApplyMotorTorque();
            ApplySteering();
            ApplyBrake();
        }
        
        private void ApplySteering()
        {
            _currentSteeringAngle = _turnInput * _config.SteeringCurve.Evaluate(_currentSpeed);

            foreach (var axle in axles)
            {
                if (axle.canSteer)
                {
                    axle.ApplySteering(_currentSteeringAngle);
                }
            }
        }

        private void ApplyBrake()
        {
            foreach (var axle in axles)
            {
                if (_handbrake && axle.hasHandbrake)
                {
                    axle.ApplyBrakePower(_config.BrakeForce, true);
                }
                else
                {
                    axle.ApplyBrakePower(_brakeInput * _config.BrakeForce);
                }
            }
        }

        private void ApplyMotorTorque()
        {
            foreach (var axle in axles)
            {
                if (axle.hasMotor)
                {
                    _currentMotorTorque = _config.MaxMotorTorque * _gasInput;
                    axle.ApplyMotorTorque(_currentMotorTorque);
                }
            }
        }

        private void UpdateWheelMesh()
        {
            foreach (var axle in axles)
            {
                axle.ApplyLocalPositionToVisuals();
            }
        }

        private float CalculateGasInput(float directionInput)
        {
            if (directionInput > 0.5f)
            {
                return _gasInput > 1f ? 1f : _gasInput + _config.DirectionSmoothing * Time.deltaTime;
            }
            else if (directionInput < -0.5f)
            {
                return _gasInput < -1f ? -1f : _gasInput - _config.DirectionSmoothing * Time.deltaTime;
            }
            else
            {
                return 0;
            }
        }
        
        private float CalculateTurnInput(float turnInput)
        {
            if (turnInput > 0.5f)
            {
                return _turnInput > 1f ? 1f : _turnInput + _config.TurnSmoothing * Time.deltaTime;
            }
            else if (turnInput < -0.5f)
            {
                return _turnInput < -1f ? -1f : _turnInput - _config.TurnSmoothing * Time.deltaTime;
            }
            else
            {
                return 0;
            }
        }
        
        private void CheckInput()
        {
            _handbrake = _input.GetHandbrakeStatus();
            
            float directionInput = _input.GetDirection();
            _gasInput = CalculateGasInput(directionInput);
            
            _turnInput = CalculateTurnInput(_input.GetTurn());

            var forward = transform.forward;

            float movingDirection = Vector3.Dot(forward, carRigidbody.velocity);
            if (movingDirection < -0.5f && directionInput > 0)
            {
                _brakeInput = Mathf.Abs(directionInput);
            }
            else if (movingDirection > 0.5f && directionInput < 0)
            {
                _brakeInput = Mathf.Abs(directionInput);
            }
            else
            {
                _brakeInput = 0;
            }
        }

        private void ShowNewInfo()
        {
            if (!infoText)
                return;

            StringBuilder info = new StringBuilder();
            info.Append($"Motor Torque: {_currentMotorTorque}");
            info.Append($"\nRB speed: {_currentSpeed}");
            info.Append($"\nSteering: {_currentSteeringAngle}");
            info.Append($"\nDirection input: {_gasInput}");
            info.Append($"\nTurn input: {_turnInput}");
            //info.Append($"\nWheel RPM: {axles[0].leftWheelCollider.rpm}");

            infoText.text = info.ToString();
        }

        private void SaveMovingData()
        {
            CheckGrounded();

            var carTransform = transform;
            _movingData = new CarMovingData(carTransform.position, carTransform.rotation, _carIsGrounded);
        }

        private void CheckGrounded()
        {
            foreach (var axle in axles)
            {
                if (!axle.IsGrounded())
                {
                    _carIsGrounded = false;
                    return;
                }
            }
            
            _carIsGrounded = true;
        }

        public void Reinitialize()
        {
            _currentMotorTorque = 0;
            _currentSpeed = 0;
            _currentSteeringAngle = 0;

            // input
            _brakeInput = 0;
            _gasInput = 0;
            _turnInput = 0;

            foreach (var axle in axles)
            {
                axle.ApplySteering(0);
                axle.ApplyMotorTorque(0);
            }
            

            carRigidbody.velocity = Vector3.zero;
        }
    }
}