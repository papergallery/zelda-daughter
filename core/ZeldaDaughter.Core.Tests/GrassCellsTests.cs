using System;
using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.Scenes;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>C7: a zone of dry grass becomes a grid of grass cells for the fire (§8).</summary>
    public class GrassCellsTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static Area Rect(float cx, float cz, float sx, float sz, float rot = 0) => new Area { Shape = "rect", Center = new Pt(cx, cz), Size = new Pt(sx, sz), Rotation = rot };

        [Fact]
        public void A_rect_gives_a_grid_inside_it_with_numbered_ids()
        {
            var cells = GrassCells.Grid(Rect(10, 20, 6, 3), 1.5f, "meadow");
            Assert.Equal(4 * 2, cells.Count);
            Assert.All(cells, c => Assert.True(Rect(10, 20, 6, 3).Contains(c.Position.X, c.Position.Y)));
            Assert.Equal("meadow_000", cells[0].Id);
            Assert.Equal(cells.Count, cells.Select(c => c.Id).Distinct().Count());
        }

        [Fact]
        public void The_grid_is_deterministic_and_row_major()
        {
            var a = GrassCells.Grid(Rect(0, 0, 9, 9), 1.5f, "g");
            var b = GrassCells.Grid(Rect(0, 0, 9, 9), 1.5f, "g");
            Assert.Equal(a.Select(c => (c.Id, c.Position)), b.Select(c => (c.Id, c.Position)));
            for (int i = 1; i < a.Count; i++) Assert.True(a[i].Position.Y > a[i - 1].Position.Y || (a[i].Position.Y == a[i - 1].Position.Y && a[i].Position.X > a[i - 1].Position.X));
        }

        [Fact]
        public void A_circle_keeps_only_cells_inside()
        {
            var circle = new Area { Shape = "circle", Center = new Pt(0, 0), Radius = 5 };
            var cells = GrassCells.Grid(circle, 1.5f, "c");
            Assert.NotEmpty(cells);
            Assert.All(cells, c => Assert.True(c.Position.Length <= 5f + 1e-3f));
            Assert.InRange(cells.Count, (int)(Math.PI * 25 / 2.25 * 0.8), (int)(Math.PI * 25 / 2.25 * 1.2));
        }

        [Fact]
        public void A_zone_too_small_for_one_cell_still_gets_its_middle()
        {
            var cells = GrassCells.Grid(Rect(3, 3, 0.5f, 0.5f), 1.5f, "t");
            Assert.Single(cells);
            Assert.Equal(new Vec2(3, 3), cells[0].Position);
        }

        [Fact]
        public void A_bad_spacing_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GrassCells.Grid(Rect(0, 0, 4, 4), 0f, "x"));
        }

        [Fact]
        public void With_the_data_spacing_fire_crosses_the_whole_field()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            g.HeroPosition = new Vec2(-500, -500);
            var cells = GrassCells.Grid(Rect(0, 0, 12, 9), D.Elements.Grass.CellSpacing, "f");
            foreach (var c in cells) g.Nature.Grass.AddCell(c.Id, c.Position);
            Assert.True(g.Nature.Grass.Ignite(cells[0].Id));
            for (int i = 0; i < 4000 && cells.Any(c => g.Nature.Grass.StateOf(c.Id) == GrassState.Dry); i++) g.TickWorld(0.5f, 0.001 + (i * 0.37) % 0.002);
            Assert.All(cells, c => Assert.NotEqual(GrassState.Dry, g.Nature.Grass.StateOf(c.Id)));
        }
    }
}
