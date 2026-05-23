using FileController_v2.VC;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;


namespace FileController_v2
{
    public class CommitGraphRenderer
    {

        public MainWindow MW;
        private Canvas _canvas;
        private Repository _repo;


        public Commit _selected;
        bool Creating = true;

        private int _nextX = 0;

        private Dictionary<string, NodeInfo> _layout = new();

        private Dictionary<string, Border> _nodes = new();
        private Dictionary<string, System.Windows.Point> _centers = new();
        public CommitGraphRenderer(Canvas canvas, MainWindow MWw)
        {
            MW = MWw;
            _canvas = canvas;
            _canvas.MouseLeftButtonDown += (s, e) =>
            {
                UpdateVisuals();
                _selected = null;
                MainProgramLogic.SelectedCommit = null;
                MW.UpdateUI();
            };
        }

        public void Render(Repository repo)
        {
            _repo = repo;
           

            _canvas.Children.Clear();
            _nodes.Clear();

            if (repo?.Commits == null) return;

            BuildLayout(repo);
            UpdateCanvasSize();

            foreach (var node in _layout.Values)
            {
                var border = CreateNode(node.Commit);

                Canvas.SetLeft(border, 50 + node.X * 200); // X = ветка
                Canvas.SetTop(border, 50 + node.Y * 100);  // Y = глубина

                _centers[node.Commit.ID] = new System.Windows.Point(
                    50 + node.X * 200 + 80,   // центр X
                    50 + node.Y * 100 + 20    // центр Y
                );

                _canvas.Children.Add(border);
                _nodes[node.Commit.ID] = border;
            }
            foreach (var commit in _repo.Commits)
            {
                if (commit.ParentID == "-1") continue;
                if (!_centers.ContainsKey(commit.ID)) continue;
                if (!_centers.ContainsKey(commit.ParentID)) continue;

                DrawLine(_centers[commit.ParentID], _centers[commit.ID]);
            }

            UpdateVisuals();
        }

        private Border CreateNode(Commit commit)
        {
            var border = new Border
            {
                Width = 160,
                Height = 60,
                BorderBrush = System.Windows.Media.Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Background = System.Windows.Media.Brushes.LightGray,
                Tag = commit,
                Child = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = commit.Name,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = commit.Owner,
                            FontSize = 8,
                            Opacity = 0.7,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = commit.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                            FontSize = 8,
                            Opacity = 0.7,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = commit.ID,
                            FontSize = 8,
                            Opacity = 0.5,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                }
            };
            System.Windows.Controls.Panel.SetZIndex(border, 1);

            border.MouseLeftButtonDown += (s, e) =>
            {
                _selected = (Commit)((Border)s).Tag;
                MainProgramLogic.SelectedCommit = _selected;
                UpdateVisuals();
                MW.UpdateUI();
                e.Handled = true;
            };

            border.MouseRightButtonUp += (s, e) =>
            {
                _selected = (Commit)((Border)s).Tag;
                MainProgramLogic.SelectedCommit = _selected;
                UpdateVisuals();
                MW.UpdateUI();
                e.Handled = true;
                
                CommitControl commitControl = new(_selected, _repo);
                commitControl.ShowDialog();

            };

            return border;
        }

        private void UpdateVisuals()
        {
            if (_repo == null) return;

            foreach (var node in _nodes)
            {
                var commit = (Commit)node.Value.Tag;

                if (commit.ID == _repo.HEAD)
                {
                    node.Value.Background = System.Windows.Media.Brushes.LightGreen; // HEAD
                    if (MainProgramLogic.SelectedCommit == null && Creating) { 
                        MainProgramLogic.SelectedCommit = commit;
                        Creating = false;
                    }
                }  
                else if (_selected != null && commit.ID == _selected.ID)
                    node.Value.Background = System.Windows.Media.Brushes.Orange; // selected
                else
                    node.Value.Background = System.Windows.Media.Brushes.LightGray;
            }
        }

        private void BuildLayout(Repository repo)
        {
            _layout.Clear();
            _nextX = 0;

            var children = repo.Commits
                .GroupBy(c => c.ParentID)
                .ToDictionary(g => g.Key, g => g.ToList());

            int rootY = 0;

            void Place(Commit commit, int y)
            {
                if (_layout.ContainsKey(commit.ID))
                    return;

                int x;

                if (!children.ContainsKey(commit.ID) || children[commit.ID].Count == 0)
                {
                    // лист → просто следующий слот
                    x = _nextX++;
                }
                else
                {
                    foreach (var child in children[commit.ID])
                        Place(child, y + 1);

                    var childXs = children[commit.ID]
                        .Select(c => _layout[c.ID].X)
                        .ToList();

                    x = (int)childXs.Average();
                }

                _layout[commit.ID] = new NodeInfo
                {
                    Commit = commit,
                    X = x,
                    Y = y
                };
            }

            var roots = repo.Commits.Where(c => c.ParentID == "-1");

            foreach (var r in roots)
                Place(r, 0);
        }
        private void UpdateCanvasSize()
        {
            if (_layout == null || _layout.Count == 0)
                return;

            double maxX = 0;
            double maxY = 0;

            foreach (var node in _layout.Values)
            {
                double x = 50 + node.X * 200;
                double y = 50 + node.Y * 100;

                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            _canvas.Width = maxX + 300;
            _canvas.Height = maxY + 200;
        }

        private void DrawLine(System.Windows.Point a, System.Windows.Point b)
        {
            var line = new Line
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 2
            };
            System.Windows.Controls.Panel.SetZIndex(line, 0);

            _canvas.Children.Add(line);
        }


        private class NodeInfo
        {
            public Commit Commit;
            public int X;
            public int Y;
        }
    }
}