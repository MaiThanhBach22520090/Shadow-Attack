using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockManager : NetworkBehaviour
{
    // Depends On The Max Players, Players' BreakBlockCD And The Block Reappear Time
    // With A Reappear Time Of 3 Seconds, And A 1.25 Seconds BreakBlockCD, A Max Of 3
    // Blocks Per Player Can Be Disabled At The Same Time

    private const int MaxDisableBlocks = 12 * 3;

    [SerializeField] private float reappearTime = 3f;
    [Networked, Capacity(MaxDisableBlocks)] private NetworkArray<Block> DisabledBlocks { get; }

    private ChangeDetector changeDetector;
    private int head;
    private int tail;
    private TickTimer tickTimer;

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
        changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            Block tailBlock = DisabledBlocks.Get(tail);
            while (tailBlock != null && tailBlock.ReappearTick <= Runner.Tick)
            {
                tailBlock.Hide(false);
                tailBlock.ReappearTick = 0;
                DisabledBlocks.Set(tail, null);
                tail = (tail + 1) % MaxDisableBlocks;
                tailBlock = DisabledBlocks.Get(tail);
            }

            return;
        }

        foreach (string property in changeDetector.DetectChanges(this, out NetworkBehaviourBuffer previous, out NetworkBehaviourBuffer current))
        {
            switch (property)
            {
                case nameof(DisabledBlocks):
                    ArrayReader<Block> reader = GetArrayReader<Block>(nameof(DisabledBlocks));
                    NetworkArrayReadOnly<Block> previousArray = reader.Read(previous);
                    NetworkArrayReadOnly<Block> currentArray = reader.Read(current);

                    for (int i = 0; i < MaxDisableBlocks; i++)
                    {
                        Block previousBlock = previousArray[i];
                        Block currentBlock = currentArray[i];
                        int currentReappearTick = currentBlock == null ? -1 : currentBlock.ReappearTick;
                        int previousReappearTick = previousBlock == null ? -1 : previousBlock.ReappearTick;

                        if (currentReappearTick != previousReappearTick)
                        {
                            if (previousBlock != null)
                                previousBlock.Hide(false);
                            
                            if (currentBlock != null)
                                currentBlock.Hide(true);
                        }
                    }
                    break;
                default:
                    break;  
            }
        }
    }

    public void AddDisable(Block block)
    {
        if (!HasStateAuthority)
            return;

        Block headBlock = DisabledBlocks.Get(head);
        if (headBlock != null)
            headBlock.Hide(false);

        tickTimer = TickTimer.CreateFromSeconds(Runner, reappearTime);
        block.Hide(true);
        block.ReappearTick = tickTimer.TargetTick ?? 0;
        DisabledBlocks.Set(head, block);
        head = (head + 1) % MaxDisableBlocks;
    }
}
