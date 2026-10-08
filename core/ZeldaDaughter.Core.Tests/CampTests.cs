using System.Linq;
using ZeldaDaughter.Core.Common;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Data;
using ZeldaDaughter.Core.Remarks;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Tests
{
    /// <summary>D-06 (camp): project-design.md §6 «Крафт костра», «Отдых и сон», §7 «Размещение предметов в мире».</summary>
    public class CampTests
    {
        static readonly DataSet D = DataSet.Load(TestPaths.DataRoot);

        static GameState G()
        {
            var g = new GameState(D);
            g.Clock.SetTime(1, 12.0 / 24.0);
            return g;
        }

        static PlacedObject PlaceWood(GameState g, float x = 0, float z = 0)
        {
            g.Bag.Add("firewood", 3);
            var r = g.Camp.Place("firewood", new Vec2(x, z));
            Assert.Equal(PlaceOutcome.Placed, r.Outcome);
            return r.Object!;
        }

        static Campfire Light(GameState g, float x = 0, float z = 0)
        {
            var wood = PlaceWood(g, x, z);
            g.Bag.Add("flint");
            var u = g.Camp.Use(wood.Id, "flint");
            Assert.Equal(UseOutcome.Done, u.Outcome);
            return g.Camp.Campfires.Single(c => c.Id == wood.Id);
        }

        [Fact]
        public void Placing_takes_the_item_out_of_the_bag_and_puts_an_object_on_the_ground()
        {
            var g = G();
            g.Bag.Add("firewood", 2);
            var r = g.Camp.Place("firewood", new Vec2(3, 4));
            Assert.Equal(PlaceOutcome.Placed, r.Outcome);
            Assert.Equal(1, g.Bag.Count("firewood"));
            Assert.Equal("firewood_placed", r.Object!.Kind);
            Assert.Equal(new Vec2(3, 4), r.Object.Position);
            Assert.Single(g.Camp.Objects);
        }

        [Fact]
        public void An_invalid_surface_and_unplaceable_things_are_refused_with_a_remark_and_nothing_is_lost()
        {
            var g = G();
            g.Bag.Add("firewood");
            var wall = g.Camp.Place("firewood", new Vec2(0, 0), validSurface: false);
            Assert.Equal(PlaceOutcome.InvalidSurface, wall.Outcome);
            Assert.Equal(Topics.PlaceInvalid, wall.Topic);
            Assert.Equal(1, g.Bag.Count("firewood"));
            Assert.Empty(g.Camp.Objects);
            g.Bag.Add("coin", 3);
            var coin = g.Camp.Place("coin", new Vec2(0, 0));
            Assert.Equal(PlaceOutcome.NotPlaceable, coin.Outcome);
            Assert.Equal(Topics.PlaceInvalid, coin.Topic);
            Assert.Equal(PlaceOutcome.NoItem, g.Camp.Place("planks", new Vec2(0, 0)).Outcome);
        }

        [Fact]
        public void Two_things_do_not_go_on_the_same_spot()
        {
            var g = G();
            PlaceWood(g, 1, 1);
            Assert.Equal(PlaceOutcome.TooClose, g.Camp.Place("firewood", new Vec2(1 + D.Camp.PlaceMinSpacing / 2, 1)).Outcome);
        }

        [Fact]
        public void A_placed_thing_can_be_picked_up_again()
        {
            var g = G();
            var wood = PlaceWood(g);
            Assert.True(g.Camp.PickUp(wood.Id));
            Assert.Equal(3, g.Bag.Count("firewood"));
            Assert.Empty(g.Camp.Objects);
            Assert.False(g.Camp.PickUp(wood.Id));
        }

        [Fact]
        public void Flint_on_placed_firewood_lights_a_campfire_and_keeps_the_flint()
        {
            var g = G();
            var fire = Light(g, 2, 2);
            Assert.True(fire.IsLit);
            Assert.Equal(D.Camp.BurnSeconds, fire.BurnLeft, 3);
            Assert.Equal(new Vec2(2, 2), fire.Position);
            Assert.Equal(1, g.Bag.Count("flint"));
            Assert.Empty(g.Camp.Objects);
        }

        [Fact]
        public void Planks_and_a_stick_also_make_a_campfire_and_the_stick_is_used_up()
        {
            var g = G();
            g.Bag.Add("planks"); g.Bag.Add("stick");
            var pl = g.Camp.Place("planks", new Vec2(0, 0)).Object!;
            Assert.Equal(UseOutcome.Done, g.Camp.Use(pl.Id, "stick").Outcome);
            Assert.Equal(0, g.Bag.Count("stick"));
            Assert.Single(g.Camp.Campfires);
        }

        [Fact]
        public void Wrong_items_on_the_pile_do_nothing()
        {
            var g = G();
            var wood = PlaceWood(g);
            g.Bag.Add("berries");
            Assert.Equal(UseOutcome.NoRecipe, g.Camp.Use(wood.Id, "berries").Outcome);
            Assert.Equal(UseOutcome.MissingItem, g.Camp.Use(wood.Id, "flint").Outcome);
            Assert.Equal(UseOutcome.NoTarget, g.Camp.Use("nothing_here", "flint").Outcome);
        }

        [Fact]
        public void A_campfire_burns_for_its_time_then_goes_out_and_firewood_extends_it()
        {
            var g = G();
            var fire = Light(g);
            g.TickWorld(100, 0.99);
            Assert.True(fire.IsLit);
            g.Bag.Add("firewood", 2);
            var add = g.Camp.Use(fire.Id, "firewood");
            Assert.Equal(UseOutcome.Refueled, add.Outcome);
            Assert.Equal(3, g.Bag.Count("firewood"));            // 2 left after placing + 2 added - 1 used
            Assert.Equal(D.Camp.BurnSeconds - 100 + D.Camp.Fuel["firewood"], fire.BurnLeft, 2);
            // cap
            g.Bag.Add("firewood", 5);
            for (int i = 0; i < 5; i++) g.Camp.Use(fire.Id, "firewood");
            Assert.Equal(D.Camp.MaxBurnSeconds, fire.BurnLeft, 2);

            var h = G();
            var f2 = Light(h);
            var ev = h.TickWorld(D.Camp.BurnSeconds + 1, 0.99);
            Assert.Contains(ev, e => e.Kind == WorldEventKind.CampfireBurntOut && e.Id == f2.Id);
            Assert.Empty(h.Camp.Campfires);
        }

        [Fact]
        public void The_light_fades_in_the_last_seconds()
        {
            var g = G();
            var fire = Light(g);
            Assert.Equal(1f, fire.Light, 3);
            g.TickWorld(D.Camp.BurnSeconds - D.Camp.FadeSeconds / 2, 0.99);
            Assert.InRange(fire.Light, 0.3f, 0.7f);
        }

        [Fact]
        public void Rain_makes_a_fire_burn_down_faster()
        {
            var dry = G(); var fd = Light(dry);
            var wet = G(); var fw = Light(wet);
            wet.Nature.Weather.StartRain(1000);
            dry.TickWorld(60, 0.99); wet.TickWorld(60, 0.99);
            Assert.True(fw.BurnLeft < fd.BurnLeft - 1);
            Assert.Equal(D.Camp.BurnSeconds - 60 * D.Camp.RainBurnFactor, fw.BurnLeft, 1);
        }

        [Fact]
        public void The_rest_zone_is_around_a_lit_fire_and_heals_faster()
        {
            var g = G();
            var fire = Light(g, 10, 10);
            g.HeroPosition = new Vec2(10 + D.Camp.RestRadius - 0.1f, 10);
            Assert.Equal(RestKind.Campfire, g.CurrentRest());
            g.HeroPosition = new Vec2(10 + D.Camp.RestRadius + 0.5f, 10);
            Assert.Equal(RestKind.None, g.CurrentRest());

            // same wound, same time: by the fire it heals more
            var near = G(); Light(near, 0, 0); near.HeroPosition = new Vec2(1, 0);
            var far = G(); far.HeroPosition = new Vec2(50, 0);
            near.Condition.Wound(WoundType.Cut, 0.5f); far.Condition.Wound(WoundType.Cut, 0.5f);
            near.Condition.Damage(30); far.Condition.Damage(30);
            for (int i = 0; i < 20; i++) { near.Condition.Tick(1, near.CurrentRest()); far.Condition.Tick(1, far.CurrentRest()); }
            Assert.True(near.Condition.Hp > far.Condition.Hp);
            Assert.True(near.Condition.Severity(WoundType.Cut) < far.Condition.Severity(WoundType.Cut));
            // and once the fire is out, no rest
            near.TickWorld(D.Camp.BurnSeconds + 1, 0.99);
            Assert.Equal(RestKind.None, near.CurrentRest());
            Assert.NotNull(fire);
        }

        [Fact]
        public void A_torch_is_lit_only_from_a_burning_fire()
        {
            var g = G();
            var wood = PlaceWood(g);
            g.Bag.Add("torch_unlit");
            var cold = g.Camp.Use(wood.Id, "torch_unlit");
            Assert.NotEqual(UseOutcome.Done, cold.Outcome);
            Assert.Equal(1, g.Bag.Count("torch_unlit"));

            g.Bag.Add("flint");
            g.Camp.Use(wood.Id, "flint");
            var lit = g.Camp.Use(wood.Id, "torch_unlit");
            Assert.Equal(UseOutcome.Done, lit.Outcome);
            Assert.Equal(1, g.Bag.Count("torch"));
            Assert.Equal(0, g.Bag.Count("torch_unlit"));
        }

        [Fact]
        public void A_burnt_out_campfire_cannot_light_a_torch_and_says_so()
        {
            var g = G();
            var fire = Light(g);
            g.Bag.Add("torch_unlit");
            g.TickWorld(D.Camp.BurnSeconds + 5, 0.99);
            var r = g.Camp.Use(fire.Id, "torch_unlit");
            Assert.Equal(UseOutcome.NoTarget, r.Outcome);
        }

        [Fact]
        public void Meat_cooks_on_a_burning_fire_and_not_when_the_bag_is_full()
        {
            var g = G();
            var fire = Light(g);
            g.Bag.Add("meat", 2);
            var r = g.Camp.Use(fire.Id, "meat");
            Assert.Equal(UseOutcome.Done, r.Outcome);
            Assert.Equal("cooked_meat", r.Item);
            Assert.Equal(1, g.Bag.Count("cooked_meat"));
            Assert.Equal(1, g.Bag.Count("meat"));

            var h = G();
            var f2 = Light(h);
            foreach (var it in D.Items.Values.Where(i => i.Kind != "currency" && i.Kind != "quest" && i.Id != "meat" && i.Id != "cooked_meat"))
            {
                if (h.Bag.UsedSlots >= D.Inventory.Slots - 2) break;
                h.Bag.Add(it.Id);
            }
            h.Bag.Add("meat", 10);                     // two full stacks: using one frees no slot
            Assert.Equal(D.Inventory.Slots, h.Bag.UsedSlots);
            var before = SaveGame.Capture(h);
            var full = h.Camp.Use(f2.Id, "meat");
            Assert.Equal(UseOutcome.NoRoom, full.Outcome);
            Assert.Equal(before, SaveGame.Capture(h));
        }

        [Fact]
        public void Light_and_the_dark_of_night_without_a_fire()
        {
            var g = G();
            g.HeroPosition = new Vec2(0, 0);
            Assert.False(g.NightWithoutFire);          // noon
            g.Clock.SetTime(1, 0.95);
            Assert.True(g.NightWithoutFire);
            Light(g, 3, 0);
            Assert.False(g.NightWithoutFire);          // fire in sight
            Assert.True(g.Camp.IsLitNear(new Vec2(0, 0), D.Camp.LightRadius));
            Assert.False(g.Camp.IsLitNear(new Vec2(40, 0), D.Camp.LightRadius));
            var h = G();
            h.Clock.SetTime(1, 0.95);
            h.Bag.Add("torch");
            Assert.False(h.NightWithoutFire);          // a torch in hand is light too
        }

        [Fact]
        public void The_camp_survives_a_save_and_old_saves_load_without_one()
        {
            var g = G();
            Light(g, 5, 6);
            PlaceWood(g, 20, 20);
            g.TickWorld(30, 0.99);
            string a = SaveGame.Capture(g);
            var fresh = G();
            SaveGame.Restore(fresh, a);
            Assert.Equal(a, SaveGame.Capture(fresh));
            Assert.Equal(D.Camp.BurnSeconds - 30, fresh.Camp.Campfires.Single().BurnLeft, 2);
            Assert.Single(fresh.Camp.Objects);
            // ids keep counting, no clash after a load
            fresh.Bag.Add("firewood");
            var next = fresh.Camp.Place("firewood", new Vec2(30, 30)).Object!;
            Assert.DoesNotContain(fresh.Camp.Objects.Where(o => o != next), o => o.Id == next.Id);
            Assert.DoesNotContain(fresh.Camp.Campfires, c => c.Id == next.Id);

            var old = a.Replace($"\"Version\": {SaveGame.Version}", "\"Version\": 2");
            var o2 = G();
            SaveGame.Restore(o2, old);
            Assert.NotNull(o2);
        }

        [Fact]
        public void Camp_data_is_checked()
        {
            var files = System.IO.Directory.GetFiles(TestPaths.DataRoot, "*.json").ToDictionary(f => System.IO.Path.GetFileName(f)!, System.IO.File.ReadAllText);
            files["camp.json"] = files["camp.json"].Replace("\"planks\": 150", "\"plankss\": 150");
            var ex = Assert.Throws<DataException>(() => DataSet.Load(n => files[n]));
            Assert.Contains(ex.Problems, p => p.Contains("camp.json") && p.Contains("plankss"));
        }

        [Fact]
        public void Same_inputs_same_camp()
        {
            string Run()
            {
                var g = G();
                Light(g, 1, 1);
                g.Nature.Weather.StartRain(50);
                for (int i = 0; i < 100; i++) g.TickWorld(1f, (i * 0.137) % 1.0);
                return SaveGame.Capture(g);
            }
            Assert.Equal(Run(), Run());
        }
    }
}
