﻿using DeBroglie.Topo;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeBroglie.Constraints
{
    /// <summary>
    /// The MaxConsecutiveConstraint checks that no more than the specified amount of
    /// </summary>
    public class MaxConsecutiveConstraint : ITileConstraint
    {
        private TilePropagatorTileSet tileSet;

        public ISet<Tile> Tiles { get; set; }

        public int MaxCount { get; set; }

        public ISet<Axis> Axes { get; set; }

        public void Init(TilePropagator propagator)
        {
            if(propagator.Topology.Directions.Type != Topo.DirectionSetType.Cartesian2d &&
                propagator.Topology.Directions.Type != Topo.DirectionSetType.Cartesian3d)
            {
                // This wouldn't be that hard to fix
                throw new Exception("MaxConsecutiveConstraint only supports cartesian topologies.");
            }
            tileSet = propagator.CreateTileSet(Tiles);
        }

        public void Check(TilePropagator propagator)
        {
            var topology = propagator.Topology;
            var width = topology.Width;
            var height = topology.Height;
            var depth = topology.Depth;

            if (Axes == null || Axes.Contains(Axis.X))
            {
                int y = 0, z = 0;
                var sm = new StateMachine((x) => propagator.Ban(x, y, z, tileSet), propagator.Topology.PeriodicX, width, MaxCount);

                for (z = 0; z < depth; z++)
                {
                    for (y = 0; y < height; y++)
                    {
                        sm.Reset();
                        for (var x = 0; x < width; x++)
                        {
                            propagator.GetBannedSelected(x, y, z, tileSet, out var isBanned, out var isSelected);
                            if (sm.Next(x, isBanned, isSelected))
                            {
                                propagator.SetContradiction();
                                return;
                            }
                        }
                        if (propagator.Topology.PeriodicX)
                        {
                            for (var x = 0; x < MaxCount && x < width; x++)
                            {
                                propagator.GetBannedSelected(x, y, z, tileSet, out var isBanned, out var isSelected);
                                if (sm.Next(x, isBanned, isSelected))
                                {
                                    propagator.SetContradiction();
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // Same thing as XAxis, just swizzled
            if (Axes == null || Axes.Contains(Axis.Y))
            {
                int x = 0, z = 0;
                var sm = new StateMachine((y) => propagator.Ban(x, y, z, tileSet), propagator.Topology.PeriodicY, height, MaxCount);

                for (z = 0; z < depth; z++)
                {
                    for (x = 0; x < width; x++)
                    {
                        sm.Reset();
                        for (var y = 0; y < height; y++)
                        {
                            propagator.GetBannedSelected(x, y, z, tileSet, out var isBanned, out var isSelected);
                            if (sm.Next(y, isBanned, isSelected))
                            {
                                propagator.SetContradiction();
                                return;
                            }
                        }
                        if (propagator.Topology.PeriodicY)
                        {
                            for (var y = 0; y < MaxCount && y < height; y++)
                            {
                                propagator.GetBannedSelected(x, y, z, tileSet, out var isBanned, out var isSelected);
                                if (sm.Next(y, isBanned, isSelected))
                                {
                                    propagator.SetContradiction();
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // Same thing as XAxis, just swizzled
            if (Axes == null || Axes.Contains(Axis.Z))
            {
                int x = 0, y = 0;
                var sm = new StateMachine((z) => propagator.Ban(x, y, z, tileSet), propagator.Topology.PeriodicZ, depth, MaxCount);

                for (y = 0; y < height; y++)
                {
                    for (x = 0; x < width; x++)
                    {
                        sm.Reset();
                        for (var z = 0; z < depth; z++)
                        {
                            propagator.GetBannedSelected(x, y, z, tileSet, out var isBanned, out var isSelected);
                            if (sm.Next(z, isBanned, isSelected))
                            {
                                propagator.SetContradiction();
                                return;
                            }
                        }
                        if (propagator.Topology.PeriodicZ)
                        {
                            for (var z = 0; z < MaxCount && z < depth; z++)
                            {
                                propagator.GetBannedSelected(x, y, z, tileSet, out var isBanned, out var isSelected);
                                if (sm.Next(z, isBanned, isSelected))
                                {
                                    propagator.SetContradiction();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Internal for testing
        // This class is a bit fiddly, but esentially it looks at at every tile
        // along an axis on-line, and tracks enough information to emit bans stopping the constraint
        // from being violated. It also returns false if the constraint is already violated.
        // There's two cases to consider:
        // 1) A run of contiguous selected tiles of length max. 
        //    Then we want to ban the tiles at either end.
        // 2) Two runs of selected with a total length of at least max-1, separated by a single tile. 
        //    Then we want to ban the center tile.
        // For periodic topologies after running over an axis, the first max tiles need a second iteration
        // to cover all looping cases.
        internal struct StateMachine
        {
            private readonly Action<int> banAt;
            private bool periodic;
            private readonly int indexCount;
            private int max;
            private State state;
            private int runCount;
            private int runStartIndex;
            private int prevRunCount;

            public StateMachine(Action<int> banAt, bool periodic, int indexCount, int max)
            {
                this.banAt = banAt;
                this.periodic = periodic;
                this.indexCount = indexCount;
                this.max = max;
                state = State.Initial;
                runCount = 0;
                runStartIndex = 0;
                prevRunCount = 0;
            }

            public void Reset()
            {
                state = State.Initial;
                runCount = 0;
                runStartIndex = 0;
                prevRunCount = 0;
            }

            public bool Next(int index, bool isBanned, bool isSelected)
            {
                switch (state)
                {
                    case State.Initial:
                        if (isSelected)
                        {
                            state = State.InRun;
                            runCount = 1;
                            runStartIndex = index;
                        }
                        return false;
                    case State.JustAfterRun:
                        if (isSelected)
                        {
                            state = State.InRun;
                            runCount = 1;
                            runStartIndex = index;
                            goto checkCases;
                        }
                        else
                        {
                            state = State.Initial;
                            prevRunCount = 0;
                            runCount = 0;
                        }
                        return false;
                    case State.InRun:
                        if(isSelected)
                        {
                            state = State.InRun;
                            runCount += 1;
                            if(runCount > max)
                            {
                                // Immediate contradiction
                                return true;
                            }
                            goto checkCases;
                        }
                        else
                        {
                            // Also case 1.
                            if (runCount == max)
                            {
                                if (!isBanned)
                                {
                                    banAt(index);
                                }
                            }
                            state = State.JustAfterRun;
                            prevRunCount = runCount;
                            runCount = 0;
                        }
                        return false;
                }
                // Unreachable
                throw new Exception("Unreachable");
                checkCases:
                    // Have we entered case 1 or 2?
                    if (prevRunCount + runCount == max)
                    {
                        // Ban on the previous end of the run
                        if (runStartIndex == 0)
                        {
                            if (periodic)
                            {
                                banAt(indexCount - 1);
                            }
                        }
                        else
                        {
                            banAt(runStartIndex - 1);
                        }
                    }
                return false;
            }

            enum State
            {
                Initial,
                InRun,
                JustAfterRun,
            }
        }
    }
}
