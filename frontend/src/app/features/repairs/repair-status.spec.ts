import { REPAIR_STATUSES, repairStatusColorClass } from './repair-status';

describe('repair statuses', () => {
  it('lists the 10 legacy states in ordinal order', () => {
    expect(REPAIR_STATUSES).toHaveLength(10);
    expect(REPAIR_STATUSES[0]).toBe('New');
    expect(REPAIR_STATUSES[9]).toBe('AssistanceContacted');
  });

  it('maps every state to a chip colour class', () => {
    for (const status of REPAIR_STATUSES) {
      expect(repairStatusColorClass(status)).toMatch(/^status-chip--/);
    }
  });
});
