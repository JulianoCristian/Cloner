﻿using UnityEngine;
using Klak.Chromatics;

namespace Cloner
{
    [ExecuteInEditMode]
    public sealed class ClonerRenderer : MonoBehaviour
    {
        #region Point source properties

        [SerializeField] PointCloud _pointSource;

        public PointCloud pointSource {
            get { return _pointSource; }
        }

        #endregion

        #region Template properties

        [SerializeField] Mesh _template;

        public Mesh template {
            get { return _template; }
        }

        [SerializeField] float _templateScale = 0.05f;

        public float templateScale {
            get { return _templateScale; }
            set { _templateScale = value; }
        }

        [SerializeField] float _scaleByNoise = 0.1f;

        public float scaleByNoise {
            get { return _scaleByNoise; }
            set { _scaleByNoise = value; }
        }

        #endregion

        #region Noise field properties

        [SerializeField] float _noiseFrequency = 1;

        public float noiseFrequency {
            get { return _noiseFrequency; }
            set { _noiseFrequency = value; }
        }

        [SerializeField] Vector3 _noiseMotion = Vector3.up * 0.25f;

        public Vector3 noiseMotion {
            get { return _noiseMotion; }
            set { _noiseMotion = value; }
        }

        [SerializeField, Range(0, 1)] float _normalModifier = 0.125f;

        public float normalModifier {
            get { return _normalModifier; }
            set { _normalModifier = value; }
        }

        #endregion

        #region Material properties

        [SerializeField] Material _material;

        public Material material {
            get { return _material; }
        }

        [SerializeField] CosineGradient _gradient;

        public CosineGradient gradient {
            get { return _gradient; }
            set { _gradient = value; }
        }

        #endregion

        #region Hidden attributes

        [SerializeField, HideInInspector] ComputeShader _compute;

        #endregion

        #region Private fields

        ComputeBuffer _drawArgsBuffer;
        ComputeBuffer _positionBuffer;
        ComputeBuffer _normalBuffer;
        ComputeBuffer _tangentBuffer;
        ComputeBuffer _transformBuffer;
        MaterialPropertyBlock _props;
        Bounds _bounds;
        Vector3 _noiseOffset;

        #endregion

        #region Compute configurations

        const int kThreadCount = 64;

        int ThreadGroupCount {
            get { return Mathf.Max(1, _pointSource.pointCount / kThreadCount); }
        }

        int InstanceCount {
            get { return ThreadGroupCount * kThreadCount; }
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            // Initialize the indirect draw args buffer.
            _drawArgsBuffer = new ComputeBuffer(
                1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
            );

            _drawArgsBuffer.SetData(new uint[5] {
                _template.GetIndexCount(0), (uint)InstanceCount, 0, 0, 0
            });

            // Allocate compute buffers.
            _positionBuffer = _pointSource.CreatePositionBuffer();
            _normalBuffer = _pointSource.CreateNormalBuffer();
            _tangentBuffer = _pointSource.CreateTangentBuffer();
            _transformBuffer = new ComputeBuffer(InstanceCount, 3 * 4 * 4);

            if (_props == null)
            {
                // This property block is used only for avoiding an instancing bug.
                _props = new MaterialPropertyBlock();
                _props.SetFloat("_UniqueID", Random.value);
            }

            // Slightly expand the bounding box.
            _bounds = _pointSource.bounds;
            _bounds.Expand(_bounds.extents * 0.25f);
        }

        void OnDisable()
        {
            _drawArgsBuffer.Release();
            _positionBuffer.Release();
            _normalBuffer.Release();
            _tangentBuffer.Release();
            _transformBuffer.Release();
        }

        void Update()
        {
            // Move the noise field.
            if (Application.isPlaying)
                _noiseOffset += _noiseMotion * Time.deltaTime;

            // Invoke the update compute kernel.
            var kernel = _compute.FindKernel("ClonerUpdate");

            _compute.SetInt("InstanceCount", InstanceCount);
            _compute.SetFloat("BaseScale", _templateScale);
            _compute.SetFloat("ScaleNoise", _scaleByNoise);
            _compute.SetFloat("NoiseFrequency", _noiseFrequency);
            _compute.SetVector("NoiseOffset", _noiseOffset);
            _compute.SetFloat("NormalModifier", _normalModifier);

            _compute.SetBuffer(kernel, "PositionBuffer", _positionBuffer);
            _compute.SetBuffer(kernel, "NormalBuffer", _normalBuffer);
            _compute.SetBuffer(kernel, "TangentBuffer", _tangentBuffer);
            _compute.SetBuffer(kernel, "TransformBuffer", _transformBuffer);

            _compute.Dispatch(kernel, ThreadGroupCount, 1, 1);

            // Draw the template mesh with instancing.
            _material.SetVector("_GradientA", _gradient.coeffsA);
            _material.SetVector("_GradientB", _gradient.coeffsB);
            _material.SetVector("_GradientC", _gradient.coeffsC2);
            _material.SetVector("_GradientD", _gradient.coeffsD2);

            _material.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
            _material.SetMatrix("_WorldToLocal", transform.worldToLocalMatrix);

            _material.SetInt("_InstanceCount", InstanceCount);
            _material.SetBuffer("_TransformBuffer", _transformBuffer);

            Graphics.DrawMeshInstancedIndirect(
                _template, 0, _material, _bounds,
                _drawArgsBuffer, 0, _props
            );
        }

        #endregion
    }
}
