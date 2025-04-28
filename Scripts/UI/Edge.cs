using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoasterForge.UI {
    public class Edge : VisualElement {
        private const float NORMAL_WIDTH = 2f;
        private const float HOVER_WIDTH = 4f;

        private static readonly Color s_EdgeColor = new(0.8f, 0.7f, 0.2f);
        private static readonly Color s_SelectedEdgeColor = new(0.2f, 0.5f, 0.9f);

        private NodeGraphView _view;
        private Entity _entity;
        private NodeGraphPort _source;
        private NodeGraphPort _target;
        private Vector2 _dragEnd;
        private Vector2 _start;
        private Vector2 _end;
        private Color _color = s_EdgeColor;
        private float _width = NORMAL_WIDTH;
        private bool _selected;

        public Entity Entity => _entity;
        public NodeGraphPort Source => _source;
        public NodeGraphPort Target => _target;

        public bool Selected {
            get => _selected;
            set {
                _selected = value;
                if (_selected) {
                    Select();
                }
                else {
                    Deselect();
                }
            }
        }

        public Edge(NodeGraphView view, Entity entity, NodeGraphPort source, NodeGraphPort target = null) {
            _view = view;
            _entity = entity;
            _source = source;
            _target = target;

            style.position = Position.Absolute;
            style.width = 0f;
            style.height = 0f;
            generateVisualContent += OnGenerateVisualContent;

            _source.Node.RegisterCallback<GeometryChangedEvent>(OnNodeMoved);
            _target?.Node.RegisterCallback<GeometryChangedEvent>(OnNodeMoved);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<MouseDownEvent>(OnMouseDown);

            UpdateBounds();
        }

        private void Select() {
            _selected = true;
            _color = s_SelectedEdgeColor;
            MarkDirtyRepaint();
        }

        private void Deselect() {
            _selected = false;
            _color = s_EdgeColor;
            MarkDirtyRepaint();
        }

        private void OnMouseEnter(MouseEnterEvent evt) {
            _width = HOVER_WIDTH;
            MarkDirtyRepaint();
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            _width = NORMAL_WIDTH;
            MarkDirtyRepaint();
        }

        private void OnNodeMoved(GeometryChangedEvent evt) {
            UpdateBounds();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 || evt.button == 1) {
                if (evt.shiftKey) {
                    if (Selected) {
                        _view.DeselectEdge(this);
                    }
                    else {
                        _view.SelectEdge(this, true);
                    }
                }
                else if (!Selected) {
                    _view.SelectEdge(this, false);
                }
            }

            if (evt.button == 1) {
                this.ShowContextMenu(evt.localMousePosition, menu => {
                    menu.AddItem("Delete", () => {
                        _view.InvokeRemoveSelectedRequest();
                    });
                });
            }
        }

        public void SetTarget(NodeGraphPort target) {
            _target?.UnregisterCallback<GeometryChangedEvent>(OnNodeMoved);
            _target = target;
            _target?.RegisterCallback<GeometryChangedEvent>(OnNodeMoved);
            UpdateBounds();
        }

        public void SetDragEnd(Vector2 dragEnd) {
            _dragEnd = dragEnd;
            UpdateBounds();
        }

        private void UpdateBounds() {
            Vector2 start = _source.Connector.worldBound.center;
            Vector2 end = _target == null ? _dragEnd : _target.Connector.worldBound.center;

            start = _view.WorldToLocal(start);
            end = _view.WorldToLocal(end);

            start = (start - _view.Offset) / _view.Zoom;
            end = (end - _view.Offset) / _view.Zoom;

            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);

            const float padding = 16f;
            min -= new Vector2(padding, padding);
            max += new Vector2(padding, padding);

            Vector2 size = max - min;
            Vector2 center = (start + end) / 2f;

            style.left = center.x - size.x * 0.5f;
            style.top = center.y - size.y * 0.5f;
            style.width = size.x;
            style.height = size.y;

            _start = start - (center - size * 0.5f);
            _end = end - (center - size * 0.5f);

            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx) {
            float dist = Mathf.Abs(_end.x - _start.x);
            float maxWidth = dist * 0.5f;
            float width = Mathf.Min(maxWidth, 50f);
            float dx = _source.Data.IsInput ? -width : width;
            Vector2 control1 = _start + new Vector2(dx, 0f);
            Vector2 control2 = _end - new Vector2(dx, 0f);

            var painter = ctx.painter2D;
            painter.lineWidth = _width;
            painter.strokeColor = _color;

            painter.BeginPath();
            painter.MoveTo(_start);
            painter.BezierCurveTo(control1, control2, _end);
            painter.Stroke();
        }
    }
}
