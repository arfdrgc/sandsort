using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// First-level drag tutorial (2026-09-23): a looping hand gesture that presses on a draggable shape,
// drags it up toward the sand and releases, over and over, until the player has done it themselves.
//
// Read-only on gameplay: it only looks at the Level's Containers and Board (position, shape, drag /
// awake state) and never touches input, drag or extraction. The gesture's start is re-read every
// frame from the shape's live cells, so it always starts on the shape wherever it currently sits.
//
// - While any shape is held the hand is hidden, so it never covers the player's own drag.
// - Done once the shape it points at has been picked up (awake) and rests in the board's top row —
//   the row that pulls sand. Then the whole tutorial object switches itself off.
// - If that shape seals or disappears first, it re-targets another open shape.
public class Tutorial1 : LevelTutorial {
	[SerializeField] TutorialHand _hand;
	[SerializeField] TextMeshProUGUI _text;
	[SerializeField] float _startDelay = 0.5f;
	[SerializeField] float _moveDuration = 0.9f;
	[SerializeField] float _endDelay = 0.4f;
	// World-space gap between the text's top edge and the bottom of the board.
	[SerializeField] float _textGap = 0.3f;
	// Where the fingertip sits inside the hand sprite, normalised (0,0 = bottom-left of the image).
	[SerializeField] Vector2 _fingertip = new Vector2(0.34f, 0.49f);

	Board _board;
	RectTransform _handImage;
	List<Container> _containers = new();
	Container _target;
	bool _handPlaying;

	public override void init(ILevel level) {
		MonoBehaviour levelBehaviour = level as MonoBehaviour;
		if (levelBehaviour == null) {
			gameObject.SetActive(false);
			return;
		}

		_board = levelBehaviour.GetComponentInChildren<Board>();
		levelBehaviour.GetComponentsInChildren(_containers);
		Image handImage = _hand.GetComponentInChildren<Image>(true);
		if (handImage != null) _handImage = handImage.rectTransform;
		_hand.hide();

		if (_board == null || !pickTarget()) {
			gameObject.SetActive(false);
			return;
		}

		if (_text != null) {
			Bounds floor = _board.floorBounds;
			_text.transform.position = new Vector3(floor.center.x, floor.min.y - _textGap, floor.center.z);
			// The prefab's canvas lies flat (90° on X), which the tilted camera sees upside down; face the
			// camera the same way TutorialHand does so the text reads normally.
			_text.transform.rotation = Camera.main.transform.rotation;
		}
	}

	void Update() {
		if (_board == null) return;

		if (_target == null || _target.isSealed || !_target.gameObject.activeInHierarchy) {
			if (!pickTarget()) {
				gameObject.SetActive(false);
				return;
			}
		}

		if (anyDragging()) {
			stopHand();
			return;
		}

		if (_target.isAwake && touchesTopRow(_target)) {
			gameObject.SetActive(false);
			return;
		}

		playHand();
	}

	void playHand() {
		if (_handPlaying) return;
		_handPlaying = true;
		// show() places the inner hand image while the drag loop moves the TutorialHand root, so put the
		// root on the shape first; otherwise the image keeps a stale offset from the root's old position.
		Vector3 start = handStart();
		_hand.transform.position = start;
		_hand.show(start);
		_hand.dragAnimation(handStart, handEnd, _startDelay, _moveDuration, _endDelay);
	}

	// The hand is positioned by its pivot, not its fingertip; shift it so the fingertip lands on the point.
	Vector3 handStart() => shapeCenter(_target) - fingertipOffset();

	Vector3 handEnd() => dragEnd() - fingertipOffset();

	// World offset from the hand image's pivot to the fingertip drawn in its sprite.
	Vector3 fingertipOffset() {
		if (_handImage == null) return Vector3.zero;
		Vector2 tip = Rect.NormalizedToPoint(_handImage.rect, _fingertip);
		return _handImage.TransformVector(tip);
	}

	// hide() deactivates the hand, which also stops its drag coroutine; playHand starts a fresh loop.
	void stopHand() {
		if (!_handPlaying) return;
		_handPlaying = false;
		_hand.hide();
	}

	// The open shape nearest the sand (highest top cell), leftmost on ties.
	bool pickTarget() {
		_target = null;
		int bestTop = int.MinValue;
		float bestX = float.MaxValue;
		foreach (Container container in _containers) {
			if (container == null || container.isSealed || !container.gameObject.activeInHierarchy) continue;
			int top = topRowOf(container);
			float x = container.gridPosition.x;
			if (top > bestTop || (top == bestTop && x < bestX)) {
				_target = container;
				bestTop = top;
				bestX = x;
			}
		}
		stopHand();
		return _target != null;
	}

	bool anyDragging() {
		foreach (Container container in _containers) {
			if (container != null && container.isDragging) return true;
		}
		return false;
	}

	int topRowOf(Container container) => container.gridPosition.y + Board.shapeMax(container.shape).y;

	bool touchesTopRow(Container container) => topRowOf(container) >= _board.topRow;

	// Centre of the shape's drawn cells, so the press lands on the shape itself for any footprint.
	Vector3 shapeCenter(Container container) {
		Vector3 sum = Vector3.zero;
		int count = container.shape.Count;
		for (int i = 0; i < count; i++) sum += container.cellVisual(i).position;
		return sum / count;
	}

	// Up the board into the top row, then one more cell so the hand clearly reaches into the sand.
	Vector3 dragEnd() {
		int rows = Mathf.Max(0, _board.topRow - topRowOf(_target)) + 1;
		return shapeCenter(_target) + _board.transform.up * (rows * _board.cellSize);
	}
}
