using System;
using ZeldaDaughter.Core.Combat;
using ZeldaDaughter.Core.Economy;
using ZeldaDaughter.Core.Journal;
using ZeldaDaughter.Core.Loot;
using ZeldaDaughter.Core.Npcs;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.Save;
using ZeldaDaughter.Core.World;
using CoreConditionEvent = ZeldaDaughter.Core.Condition.ConditionEvent;

namespace ZeldaDaughter.Game
{
    /// <summary>
    /// The bus between the session and the presenters (docs/demo/unity-architecture.md §2.2): a plain C# class, one per session
    /// (<c>GameSession.Events</c>). Everything declared here, so a package only subscribes and raises; a link between packages is an event,
    /// never a reference. <c>Raise…</c> is how an event is fired from outside the class (C# lets only the owner invoke an event).
    /// <para><b>StateReady</b> replays: a subscriber added after the state was loaded is called at once — the order of Awake/Start between
    /// components does not matter. Everything else is not replayed.</para>
    /// </summary>
    public sealed partial class SessionEvents
    {
        private GameState _ready;
        private Action<GameState> _stateReady;

        /// <summary>The state is loaded and registered with the scene — presenters take their reference to <c>g</c> only from here.</summary>
        public event Action<GameState> StateReady
        {
            add { _stateReady += value; if (_ready != null) value(_ready); }
            remove { _stateReady -= value; }
        }
        public void RaiseStateReady(GameState g) { _ready = g; _stateReady?.Invoke(g); }
        public bool IsReady => _ready != null;

        // ---- time and world ----
        public event Action<ClockEvent> Clock;
        public void RaiseClock(ClockEvent e) => Clock?.Invoke(e);

        /// <summary>Hours that passed at once (sleep, knockout): NPCs snap, fires and weather catch up.</summary>
        public event Action<double> TimeJumped;
        public void RaiseTimeJumped(double hours) => TimeJumped?.Invoke(hours);

        public event Action<WorldEvent> World;
        public void RaiseWorld(WorldEvent e) => World?.Invoke(e);

        /// <summary>An NPC's slot changed; <c>snap</c> — put it at the place at once instead of walking (first sync, after sleep).</summary>
        public event Action<NpcChange, bool> NpcMoved;
        public void RaiseNpcMoved(NpcChange change, bool snap) => NpcMoved?.Invoke(change, snap);

        // ---- the hero ----
        public event Action<CoreConditionEvent> Condition;
        public void RaiseCondition(CoreConditionEvent e) => Condition?.Invoke(e);

        public event Action<SkillChange> Skill;
        public void RaiseSkill(SkillChange c) => Skill?.Invoke(c);

        /// <summary>The hero said a line (a remark topic, or "pickup"); the bubble shows it.</summary>
        public event Action<string, string> HeroSaid;
        public void RaiseHeroSaid(string topic, string line) => HeroSaid?.Invoke(topic, line);

        /// <summary>What the hero does with her hands — HeroView animates it, Audio plays it.</summary>
        public event Action<HeroAct> HeroActed;
        public void RaiseHeroActed(HeroAct act) => HeroActed?.Invoke(act);

        public event Action<string> BagChanged;
        public void RaiseBagChanged(string why) => BagChanged?.Invoke(why);

        public event Action<string, string> PickedUp;
        public void RaisePickedUp(string objectId, string itemId) => PickedUp?.Invoke(objectId, itemId);

        // ---- gestures that are not taps ----
        /// <summary>A long press on the hero began (the radial menu opens); <c>HeroController.FingerPosition</c> follows the finger.</summary>
        public event Action LongPressHero;
        public void RaiseLongPressHero() => LongPressHero?.Invoke();

        public event Action LongPressReleased;
        public void RaiseLongPressReleased() => LongPressReleased?.Invoke();

        // ---- fight and loot ----
        public event Action<EnemyNotice> Enemy;
        public void RaiseEnemy(EnemyNotice n) => Enemy?.Invoke(n);

        public event Action<string, StrikeResult> HeroStruck;
        public void RaiseHeroStruck(string enemyId, StrikeResult r) => HeroStruck?.Invoke(enemyId, r);

        public event Action<string, LootResult> Looted;
        public void RaiseLooted(string carcassId, LootResult r) => Looted?.Invoke(carcassId, r);

        public event Action<string> CarcassGone;
        public void RaiseCarcassGone(string id) => CarcassGone?.Invoke(id);

        // ---- things put in the world ----
        public event Action<PlacedObject> Placed;
        public void RaisePlaced(PlacedObject o) => Placed?.Invoke(o);

        public event Action<string, UseResult> UsedOnWorld;
        public void RaiseUsedOnWorld(string objectId, UseResult r) => UsedOnWorld?.Invoke(objectId, r);

        // ---- talk, trade, journal ----
        public event Action<string> TalkStarted;
        public void RaiseTalkStarted(string npcId) => TalkStarted?.Invoke(npcId);

        public event Action<string> TalkEnded;
        public void RaiseTalkEnded(string npcId) => TalkEnded?.Invoke(npcId);

        public event Action<string, string> TradeRequested;
        public void RaiseTradeRequested(string npcId, string itemId) => TradeRequested?.Invoke(npcId, itemId);

        public event Action<TradeResult> Traded;
        public void RaiseTraded(TradeResult r) => Traded?.Invoke(r);

        public event Action<string> MarkOpened;
        public void RaiseMarkOpened(string id) => MarkOpened?.Invoke(id);

        public event Action<NotebookEntry> NoteAdded;
        public void RaiseNoteAdded(NotebookEntry e) => NoteAdded?.Invoke(e);

        public event Action<string> QuestDone;
        public void RaiseQuestDone(string id) => QuestDone?.Invoke(id);

        // ---- windows ----
        public event Action<string> WindowOpened;
        public void RaiseWindowOpened(string id) => WindowOpened?.Invoke(id);

        public event Action<string> WindowClosed;
        public void RaiseWindowClosed(string id) => WindowClosed?.Invoke(id);
    }
}
