using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngineInternal;

namespace GDD3400.Project01
{
    public class Dog : MonoBehaviour
    {
        
        private bool _isActive = true;
        public bool IsActive 
        {
            get => _isActive;
            set => _isActive = value;
        }

        // Required Variables (Do not edit!)
        private float _maxSpeed = 5f;
        private float _sightRadius = 7.5f;

        // Layers - Set In Project Settings
        private LayerMask _targetsLayer;
        private LayerMask _obstaclesLayer;

        // Tags - Set In Project Settings
        private string friendTag = "Friend";
        private string threatTag = "Threat";
        private string safeZoneTag = "SafeZone";

        private Vector3 _safeZone;

        // Gets rigidbody component so that dog can move.
        private Rigidbody _rb;

        // list of points to explore and ones that it has visited
        List<Vector3> explorationPoints;
        HashSet<Vector3> visitedPoints;

        public GameObject sheep;

        // Directory so that the dog remembers sheep locations
        Dictionary<Sheep, Vector3> sheepMemory;

        private Vector3 _currentExplorationTarget;
        private bool _hasTarget = false;

        // States the dog can be in
        enum DogState { Explore, ChaseSheep, HerdSheep }

        private DogState _state;

        public string _stateDebug;

        // Initialize layer masks for targets and obstacles
        // Get the Rigidbody component for movement
        // Start coroutine to remember the safe zone position
        public void Awake()
        {
            
            _targetsLayer = LayerMask.GetMask("Targets");
            _obstaclesLayer = LayerMask.GetMask("Obstacles");

            _rb = GetComponent<Rigidbody>();

            StartCoroutine(RememberSafeZone());
 
        }

        // Set initial dog state to exploring
        // Initialize data structures for exploration and memory
        void Start()
        {
            
            _state = DogState.Explore;
            explorationPoints = new List<Vector3>();
            visitedPoints = new HashSet<Vector3>();
            sheepMemory = new Dictionary<Sheep, Vector3>();

            
        }

        private void Update()
        {
            if (!_isActive) return;
            
            Perception();
            DecisionMaking();
        }

        // Use a spherical overlap check to detect sheep in the environment
        // Store or update each detected sheep's position in memory
        private void Perception()
        {
            
            Collider[] hits = Physics.OverlapSphere(transform.position, _sightRadius, _targetsLayer);


            foreach (var hit in hits)
            {
                Sheep detectedSheep = hit.GetComponent<Sheep>();
                if (detectedSheep != null)
                {
                    if (!sheepMemory.ContainsKey(detectedSheep))
                        sheepMemory.Add(detectedSheep, detectedSheep.transform.position);
                    else
                        sheepMemory[detectedSheep] = detectedSheep.transform.position;
                }
            }
        }

        // Remove sheep from memory if they're null or close to the safe zone
        // Choose state based on known sheep: Explore, Chase, or Herd
        private void DecisionMaking()
        {

            List<Sheep> toForget = new List<Sheep>();
            foreach (var kvp in sheepMemory)
            {
                Vector3 sheepPos = kvp.Key.GetComponent<Rigidbody>().position;
                float distToSafe = Vector3.Distance(sheepPos, _safeZone);

                if (kvp.Key == null || distToSafe < 7.5f ) toForget.Add(kvp.Key);
            }
            foreach (var sheep in toForget)
                sheepMemory.Remove(sheep);

            if (sheepMemory.Count == 0)
            {
                _state = DogState.Explore;
            }
            else
            {
                
                foreach (var kvp in sheepMemory)
                {
                    float dist = Vector3.Distance(transform.position, kvp.Value);
                    if (dist <= _sightRadius)
                    {
                        _state = DogState.HerdSheep;
                        return;
                    }
                }


                _state = DogState.ChaseSheep;
            }
        }

        /// <summary>
        /// Make sure to use FixedUpdate for movement with physics based Rigidbody
        /// You can optionally use FixedDeltaTime for movement calculations, but it is not required since fixedupdate is called at a fixed rate
        /// </summary>
        private void FixedUpdate()
        {
            if (!_isActive) return;

            switch (_state)
            {
                case DogState.Explore:
                    Wander();
                    break;
                case DogState.ChaseSheep:
                    MoveToClosestKnownSheep();
                    break;
                case DogState.HerdSheep:
                    HerdNearestSheep();
                    break;
            }

            _stateDebug = _state.ToString();
        }

        // Delay briefly to allow dog to settle at its start position
        // Save current position as the safe zone location
        // Enable dog behavior
        private IEnumerator RememberSafeZone()
        {
 
            yield return new WaitForSeconds(0.2f);

            _safeZone = _rb.position;
            Debug.Log(_safeZone.ToString());

            IsActive = true;
        }


        // If all exploration points are visited, generate new ones
        // Move toward the next unvisited point
        private void Wander()
        {
            
            if (explorationPoints.Count == 0 || visitedPoints.Count == explorationPoints.Count)
            {
                GenerateRandomExplorationPoints();
                visitedPoints.Clear(); 
                _hasTarget = false;
            }

            if (!_hasTarget || Vector3.Distance(transform.position, _currentExplorationTarget) < 2f)
            {
                _currentExplorationTarget = GetNextUnvisitedPoint();
                _hasTarget = true;
            }

            MoveTowards(_currentExplorationTarget);
        }

        // Find and move toward the closest sheep position in memory
        private void MoveToClosestKnownSheep()
        {
            Vector3 closest = Vector3.zero;
            float minDist = float.MaxValue;

            foreach (var pos in sheepMemory.Values)
            {
                float dist = Vector3.Distance(transform.position, pos);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = pos;
                }
            }

            MoveTowards(closest);
        }

        // Find the nearest sheep and move to a position behind it to drive it toward the safe zone
        private void HerdNearestSheep()
        {
            Sheep targetSheep = null;
            float closestDist = float.MaxValue;

            foreach (var kvp in sheepMemory)
            {
                if (kvp.Key == null) continue;

                float dist = Vector3.Distance(transform.position, kvp.Key.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetSheep = kvp.Key;
                }
            }

            if (targetSheep == null) return;

            
            Vector3 directionToSafe = (_safeZone - targetSheep.transform.position).normalized;
            Vector3 herdingPosition = targetSheep.transform.position - directionToSafe * 6f;

            
            MoveTowards(herdingPosition);
        }


        // Move the dog toward the given position using Rigidbody physics
        private void MoveTowards(Vector3 target)
        {
            Vector3 direction = (target - transform.position).normalized;
            Vector3 velocity = direction * _maxSpeed;

            direction.y = 0;

            Vector3 lookDirection = Vector3.RotateTowards(transform.forward, direction, (_maxSpeed * Time.deltaTime), 0.0f);
            
            transform.rotation = Quaternion.LookRotation(lookDirection);
            _rb.linearVelocity = Vector3.ClampMagnitude(velocity, _maxSpeed);
        }

        // Generate 10 random points within the defined area for exploration
        private void GenerateRandomExplorationPoints()
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 point = new Vector3(
                    Random.Range(-25f, 25f),
                    0.5f,
                    Random.Range(-25f, 25f)
                );

               explorationPoints.Add( point );
            }
        }

        // Return the next unvisited exploration point, or safe zone if all have been visited
        private Vector3 GetNextUnvisitedPoint()
        {
            foreach (var point in explorationPoints)
            {
                if (!visitedPoints.Contains(point))
                {
                    visitedPoints.Add(point);
                    return point;
                }
            }

            return _safeZone; 
        }

    }
}
