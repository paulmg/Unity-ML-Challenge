using Game.Missiles;
using Game.Spaceship;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MissileDodgerAgent : Agent {

  #region Variables

  public GameObject Tracker;

  public MissileLaunchScript LaunchScript;

  public Acceleration Acceleration;

  public float MaxTurnAngle = 175;
  public int Losses { get; set; }

  [SerializeField] private GameObject _academy;

  private Vector3 _startPosition;
  private readonly Vector3 _rotationAxis = Vector3.forward;

  private bool _searchingForTracker = true;

  private float _rotation;
  private float _searchRadius = 6f;
  private float _searchTargetInterval = 0.02f;
  private float _distanceFromTargetSqr;
  private float _goalTime;
  private float _lastSearchTime;
  private float _timeSinceLastTracked;
  private float _timeTracking;
  private int _wins;

  #endregion Variables

  private void Start() {
    Acceleration.TargetSpeed = 6f;
    Acceleration.CurrentSpeed = 6f;
  }

  public override void InitializeAgent() {
    _academy = GameObject.Find("Academy");
    _goalTime = _academy.GetComponent<MissileDodgerAcademy>().GoalTime;

    _startPosition = transform.position;

    AgentReset();
  }

  public override List<float> CollectState() {
    List<float> state = new List<float>();

    // Agent data
    state.Add(transform.rotation.eulerAngles.z / 180.0f - 1.0f);

//    state.Add(transform.position.normalized.x); // position x
//    state.Add(transform.position.normalized.y); // position y

    // Missile tracker data
    if (Tracker != null) {
      state.Add(1f); // Agent has missile tracker

      state.Add(Tracker.transform.childCount > 0 ? Tracker.transform.GetChild(0).transform.rotation.eulerAngles.z / 180.0f - 1.0f : 0f); // Only missile child rotates (might be useless)

//      state.Add(Tracker.transform.position.normalized.x); // position x
//      state.Add(Tracker.transform.position.normalized.y); // position y

      state.Add((Tracker.transform.position.x - transform.position.x) / _searchRadius); // normalized direction to the enemy on the X
      state.Add((Tracker.transform.position.y - transform.position.y) / _searchRadius); // normalized direction to the enemy on the Y
    }
    else {
      // Missile tracker data is set to zero
      state.Add(0f);

      state.Add(0f);
//
//      state.Add(0f);
//      state.Add(0f);

      state.Add(0f);
      state.Add(0f);
    }

    // Curriculum data
    state.Add(_academy.GetComponent<MissileDodgerAcademy>().GoalTime / 60f);
    state.Add(_academy.GetComponent<MissileDodgerAcademy>().MissileAmount / 12f);
    state.Add(_academy.GetComponent<MissileDodgerAcademy>().MissileSpeed);
    state.Add(_academy.GetComponent<MissileDodgerAcademy>().MissileFuelAmount / 20f);

    return state;
  }

  public override void AgentStep(float[] action) {
    switch ((int)action[0]) {
      // do nothing
      case 0:
        _rotation = 0f;
        reward = 0.01f; // Prefer steadier movements

        break;
      // left
      case 1:
        _rotation = 1f;

        break;

      // right
      case 2:
        _rotation = -1f;

        break;
    }

    // Countdown
    _goalTime -= Time.fixedDeltaTime;

    // Done after set time period
    if (_goalTime < 0f) {
      done = true;
      reward = 10f;
      _wins++;
    }

    // search for closest tracking missile
    if (_searchingForTracker) {
      float currentTime = Time.fixedTime;

      if (currentTime > _lastSearchTime + _searchTargetInterval) {
        Tracker = SearchForTarget();
        _timeSinceLastTracked += _searchTargetInterval;

        // Reward based on staying alive and not being tracked
        reward = Mathf.Clamp(0.01f * _timeSinceLastTracked, 0.01f, 0.04f); // Reward higher based on length since being tracked

        //check if it's too far
        if (Tracker != null) {
          _timeTracking += _searchTargetInterval;
          _timeSinceLastTracked = 0f;

          // Punish if missile near
          reward = Mathf.Clamp(-0.03f * (1f / _distanceFromTargetSqr) * _timeTracking, -0.03f, -0.01f); // Punish higher based on proximity and length of tracking

          _distanceFromTargetSqr = GetDistanceFromTargetSqr();
          if (_distanceFromTargetSqr > _searchRadius * _searchRadius) {
            //target lost
            _timeTracking = 0f;
            Tracker = null;
          }
        }

        _lastSearchTime = currentTime;
      }
    }

    // Monitors
    Monitor.Log("Reward", reward, MonitorType.text, transform);
    Monitor.Log("Rotation", _rotation, MonitorType.hist, transform);
    Monitor.Log("Goal Time", _goalTime / _academy.GetComponent<MissileDodgerAcademy>().GoalTime, MonitorType.slider);
    Monitor.Log("Wins", _wins);
    Monitor.Log("Losses", Losses);
  }

  public override void AgentReset() {
    Tracker = null;

    // Reset position and trail
    transform.position = _startPosition;
    gameObject.GetComponent<TrailRenderer>().Clear();

    // Reset times
    _timeSinceLastTracked = 0f;
    _timeTracking = 0f;
    _goalTime = _academy.GetComponent<MissileDodgerAcademy>().GoalTime;

    // Set launch options
    LaunchScript.MissileSpeed = _academy.GetComponent<MissileDodgerAcademy>().MissileSpeed;
    LaunchScript.MissilePrefab.GetComponent<MissileController>().FuelAmount = _academy.GetComponent<MissileDodgerAcademy>().MissileFuelAmount;
    LaunchScript.LauncherNodeCount = (int)_academy.GetComponent<MissileDodgerAcademy>().MissileAmount;
    LaunchScript.AutoFireInterval = _academy.GetComponent<MissileDodgerAcademy>().MissileFuelAmount - 2f;

    // Cleanup all missiles in scene
    var missiles = GameObject.FindGameObjectsWithTag("EnemyMissile");

    foreach (var missile in missiles) {
      var missileController = missile.GetComponent<MissileController>();
      missileController.DestroyMissile();
    }

    // Reset autofire
    LaunchScript.RestartAutoFire();
  }

  public override void AgentOnDone() {
  }

  private float GetDistanceFromTargetSqr() {
    return (Tracker.transform.position - transform.position).sqrMagnitude;
  }

  private GameObject SearchForTarget() {
    return GameObject
        .FindGameObjectsWithTag("EnemyMissile")
        .OrderBy(o => (o.transform.position - transform.position).sqrMagnitude)
        .FirstOrDefault(o => (o.transform.position - transform.position).sqrMagnitude < _searchRadius * _searchRadius);
  }

  private void RotationOnlyFlight() {
    var rot = _rotation;
    rot *= MaxTurnAngle * Time.deltaTime;

    transform.Rotate(_rotationAxis, rot, Space.World); // rotate the ship

    FlyStraight();
  }

  private void FlyStraight() {
    if (float.IsNaN(transform.up.x))
      transform.up = Vector3.up;

    transform.position += transform.up * Acceleration.CurrentSpeed * Time.deltaTime;
  }

  private void Update() {
    Acceleration.CalculateSpeed();

    RotationOnlyFlight();
  }
}