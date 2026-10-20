using UnityEngine;
using System;

public class MatchInput : MonoBehaviour
{
	const float PreviewPositionEpsilon = 1e-3f;
	public static MatchInput Shared { get; private set; }

	public static MatchInput GetOrCreate(Match match)
	{
		if(match == null)
			return null;

		if(Shared != null && Shared.gameObject == match.gameObject)
			return Shared;

		Shared = match.GetComponent<MatchInput>();
		if(Shared == null)
			Shared = match.gameObject.AddComponent<MatchInput>();
		return Shared;
	}

	Camera Camera => Camera.main;
	LayerMask RaycastMask => Physics.DefaultRaycastLayers;

	bool hasCursorPosition;
	Vector2 lastCursorPosition;
	bool isPrimaryDown;
	float nextPrimaryPlaceTime;

	// Touch-only controls (mobile)
	const float LongPressDuration = 0.5f;
	const float LongPressMoveTolerance = 0.05f;
	float touchStartTime;
	Vector2 touchStartPosition;
	bool longPressTriggered;
	Vector2 lastRotateTouchPosition;
	bool wasMultiTouch;

	public event Action<Vector2> OnCursorEnter;
	public event Action<Vector2> OnCursorMove;
	public event Action OnCursorExit;
	public event Action<Vector2> OnPlace;
	public event Action<Vector2> OnRemove;
	public event Action OnPass;
	public event Action<Vector2> OnRotateDrag;
	public event Action OnPrimaryDown;
	public event Action OnPrimaryUp;

	public void SubmitPass()
	{
		OnPass?.Invoke();
	}

	protected void Awake()
	{
		Shared = this;
	}

	protected void OnDestroy()
	{
		if(Shared == this)
			Shared = null;
	}

	protected void Update()
	{
		ProcessKeyboard();
		ProcessMouse();
	}

	void ProcessKeyboard()
	{
		if(Input.GetKeyDown(KeyCode.None))  // Replaced by PassTurnButtonUi
			OnPass?.Invoke();
	}

	void ProcessMouse()
	{
		// --- Touch: two-finger drag to rotate (replaces right-mouse drag) ---
		if(Input.touchCount >= 2)
		{
			Touch t0 = Input.GetTouch(0);
			Touch t1 = Input.GetTouch(1);
			if(t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began || !wasMultiTouch)
			{
				lastRotateTouchPosition = (t0.position + t1.position) * 0.5f;
				wasMultiTouch = true;
			}
			else
			{
				Vector2 currentMid = (t0.position + t1.position) * 0.5f;
				Vector2 delta = currentMid - lastRotateTouchPosition;
				if(delta.sqrMagnitude > 0)
					OnRotateDrag?.Invoke(delta);
				lastRotateTouchPosition = currentMid;
			}
			ResetPrimaryState();
			EmitCursorExitIfNeeded();
			return;
		}
		else
		{
			wasMultiTouch = false;
		}

		// --- Mouse right-click: rotate / remove ---
		if(Input.GetMouseButton(1))
		{
			Vector2 rotateDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
			if(rotateDelta.sqrMagnitude > 0)
				OnRotateDrag?.Invoke(rotateDelta);
		}

		if(Camera == null || Board.Current == null)
		{
			ResetPrimaryState();
			EmitCursorExitIfNeeded();
			return;
		}

		if(!TryGetBoardHit(out RaycastHit hit))
		{
			ResetPrimaryState();
			EmitCursorExitIfNeeded();
			return;
		}

		Vector2 absolutePosition = Board.Current.WorldToAbsolutePosition(hit.point);

		// --- Touch: long-press to remove stone (replaces right-click) ---
		if(Input.touchCount == 1)
		{
			Touch t = Input.GetTouch(0);
			if(t.phase == TouchPhase.Began)
			{
				touchStartTime = Time.unscaledTime;
				touchStartPosition = t.position;
				longPressTriggered = false;
			}
			else if(t.phase == TouchPhase.Stationary || t.phase == TouchPhase.Moved)
			{
				if(!longPressTriggered
					&& (t.position - touchStartPosition).magnitude < LongPressMoveTolerance * Screen.height
					&& Time.unscaledTime - touchStartTime >= LongPressDuration)
				{
					longPressTriggered = true;
					if(Board.Current.Topology != BoardUtility.BoardTopology.Sphere)
						OnRemove?.Invoke(absolutePosition);
				}
			}
		}

		if(!hasCursorPosition)
		{
			hasCursorPosition = true;
			lastCursorPosition = absolutePosition;
			OnCursorEnter?.Invoke(absolutePosition);
			OnCursorMove?.Invoke(absolutePosition);
		}
		else if((absolutePosition - lastCursorPosition).sqrMagnitude > PreviewPositionEpsilon)
		{
			lastCursorPosition = absolutePosition;
			OnCursorMove?.Invoke(absolutePosition);
		}

		ProcessPrimaryPlacement(absolutePosition);
		if(Input.GetMouseButtonDown(1) && Input.touchCount == 0)
		{
			if(Board.Current.Topology == BoardUtility.BoardTopology.Sphere)
				return;
			OnRemove?.Invoke(absolutePosition);
			OnCursorMove?.Invoke(absolutePosition);
		}
	}

	void ProcessPrimaryPlacement(Vector2 absolutePosition)
	{
		bool primaryDown = Input.GetMouseButton(0);
		if(!primaryDown)
		{
			ResetPrimaryState();
			return;
		}

		if(!isPrimaryDown)
		{
			isPrimaryDown = true;
			OnPrimaryDown?.Invoke();
			EmitPlace(absolutePosition);

			float frequency = GetPlacementFrequencyPerSecond();
			nextPrimaryPlaceTime = Time.unscaledTime + 1f / frequency;
			return;
		}

		if(!UseContinuousPlacement())
			return;

		if(Time.unscaledTime >= nextPrimaryPlaceTime)
		{
			EmitPlace(absolutePosition);
			float frequency = GetPlacementFrequencyPerSecond();
			nextPrimaryPlaceTime = Time.unscaledTime + 1f / frequency;
		}
	}

	void EmitPlace(Vector2 absolutePosition)
	{
		OnPlace?.Invoke(absolutePosition);
		OnCursorMove?.Invoke(absolutePosition);
	}

	void ResetPrimaryState()
	{
		if(isPrimaryDown)
			OnPrimaryUp?.Invoke();
		isPrimaryDown = false;
		nextPrimaryPlaceTime = 0f;
	}

	bool UseContinuousPlacement()
	{
		return Match.Current != null && Match.Current.UseContinuousPlacement;
	}

	float GetPlacementFrequencyPerSecond()
	{
		if(Match.Current == null)
			return 1f;
		return Mathf.Max(1f, Match.Current.ContinuousPlacementFrequencyPerSecond);
	}

	void EmitCursorExitIfNeeded()
	{
		if(!hasCursorPosition)
			return;

		hasCursorPosition = false;
		OnCursorExit?.Invoke();
	}

	bool TryGetBoardHit(out RaycastHit hit)
	{
		if(Board.Current == null)
		{
			hit = default;
			return false;
		}

		Vector3 mousePosition = Input.mousePosition;
		if(!float.IsNormal(mousePosition.sqrMagnitude))
		{
			hit = default;
			return false;
		}
		Ray ray = Camera.ScreenPointToRay(mousePosition);
		if(!Physics.Raycast(ray, out hit, Mathf.Infinity, RaycastMask, QueryTriggerInteraction.Ignore))
			return false;

		return hit.collider.transform.IsChildOf(Board.Current.transform);
	}
}
