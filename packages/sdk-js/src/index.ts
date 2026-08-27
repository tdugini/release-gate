export type RuntimeFlag = {
  key: string;
  enabled: boolean;
};

export type RuntimeSnapshot = {
  projectKey: string;
  environment: string;
  subjectKey: string;
  generatedAt: string;
  flags: RuntimeFlag[];
};

export type ReleaseGateClientOptions = {
  baseUrl: string;
  projectKey: string;
  environment: string;
  refreshInterval?: number;
  onRefreshError?: (error: unknown) => void;
  fetch?: typeof globalThis.fetch;
};

export class ReleaseGateClient {
  private readonly baseUrl: string;
  private readonly projectKey: string;
  private readonly environment: string;
  private readonly refreshInterval: number | null;
  private readonly onRefreshError?: (error: unknown) => void;
  private readonly fetchImpl: typeof globalThis.fetch;
  private subjectKey: string | null = null;
  private snapshot: RuntimeSnapshot | null = null;
  private flags = new Map<string, boolean>();
  private etag: string | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private pollingActive = false;

  constructor(options: ReleaseGateClientOptions) {
    this.baseUrl = normalizeRequired(options.baseUrl, 'baseUrl').replace(/\/+$/, '');
    this.projectKey = normalizeRequired(options.projectKey, 'projectKey');
    this.environment = normalizeRequired(options.environment, 'environment');
    this.refreshInterval = normalizeRefreshInterval(options.refreshInterval);
    this.onRefreshError = options.onRefreshError;
    this.fetchImpl = options.fetch ?? globalThis.fetch;

    if (!this.fetchImpl) {
      throw new Error('ReleaseGateClient requires a fetch implementation.');
    }
  }

  get initialized(): boolean {
    return this.snapshot !== null;
  }

  get automaticRefreshActive(): boolean {
    return this.pollingActive;
  }

  get currentSnapshot(): RuntimeSnapshot | null {
    return this.snapshot;
  }

  async initialize(subjectKey: string): Promise<RuntimeSnapshot> {
    this.stop();
    this.subjectKey = normalizeRequired(subjectKey, 'subjectKey');
    this.etag = null;

    const snapshot = await this.refresh();

    if (this.refreshInterval !== null) {
      this.start();
    }

    return snapshot;
  }

  async refresh(): Promise<RuntimeSnapshot> {
    if (!this.subjectKey) {
      throw new Error('ReleaseGateClient must be initialized with a subject key before refresh().');
    }

    const url = new URL(
      `${this.baseUrl}/api/runtime/projects/${encodeURIComponent(this.projectKey)}` +
        `/environments/${encodeURIComponent(this.environment)}/snapshot`,
    );
    url.searchParams.set('subjectKey', this.subjectKey);

    const headers = new Headers({
      Accept: 'application/json',
    });

    if (this.etag) {
      headers.set('If-None-Match', this.etag);
    }

    const response = await this.fetchImpl(url, { headers });

    if (response.status === 304) {
      if (!this.snapshot) {
        throw new Error('ReleaseGate returned 304 before the client had a cached snapshot.');
      }

      this.etag = response.headers.get('ETag') ?? this.etag;
      return this.snapshot;
    }

    if (!response.ok) {
      throw new Error(`ReleaseGate snapshot request failed with status ${response.status}.`);
    }

    const snapshot = validateSnapshot(await response.json());
    const etag = response.headers.get('ETag');

    this.snapshot = snapshot;
    this.flags = new Map(snapshot.flags.map((flag) => [flag.key, flag.enabled]));
    this.etag = etag;

    return snapshot;
  }

  start(): void {
    if (this.refreshInterval === null) {
      throw new Error('ReleaseGateClient requires refreshInterval to enable automatic refresh.');
    }

    if (!this.initialized) {
      throw new Error('ReleaseGateClient must be initialized before automatic refresh can start.');
    }

    if (this.pollingActive) {
      return;
    }

    this.pollingActive = true;
    this.scheduleNextRefresh();
  }

  stop(): void {
    this.pollingActive = false;

    if (this.refreshTimer !== null) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  isEnabled(flagKey: string, fallback = false): boolean {
    const normalizedFlagKey = flagKey.trim();
    if (!normalizedFlagKey) return fallback;

    return this.flags.get(normalizedFlagKey) ?? fallback;
  }

  private scheduleNextRefresh(): void {
    if (!this.pollingActive || this.refreshInterval === null) {
      return;
    }

    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      void this.runScheduledRefresh();
    }, this.refreshInterval);
  }

  private async runScheduledRefresh(): Promise<void> {
    try {
      await this.refresh();
    } catch (error) {
      try {
        this.onRefreshError?.(error);
      } catch {
        // Consumer callbacks must not stop the refresh loop.
      }
    } finally {
      this.scheduleNextRefresh();
    }
  }
}

function normalizeRequired(value: string, field: string): string {
  const normalized = value?.trim();
  if (!normalized) {
    throw new Error(`ReleaseGateClient option '${field}' is required.`);
  }

  return normalized;
}

function normalizeRefreshInterval(value: number | undefined): number | null {
  if (value === undefined) {
    return null;
  }

  if (!Number.isFinite(value) || value <= 0) {
    throw new Error("ReleaseGateClient option 'refreshInterval' must be greater than 0 milliseconds.");
  }

  return value;
}

function validateSnapshot(value: unknown): RuntimeSnapshot {
  if (!value || typeof value !== 'object') {
    throw new Error('ReleaseGate returned an invalid runtime snapshot.');
  }

  const snapshot = value as Partial<RuntimeSnapshot>;

  if (
    typeof snapshot.projectKey !== 'string' ||
    typeof snapshot.environment !== 'string' ||
    typeof snapshot.subjectKey !== 'string' ||
    typeof snapshot.generatedAt !== 'string' ||
    !Array.isArray(snapshot.flags) ||
    snapshot.flags.some(
      (flag) =>
        !flag ||
        typeof flag !== 'object' ||
        typeof (flag as RuntimeFlag).key !== 'string' ||
        typeof (flag as RuntimeFlag).enabled !== 'boolean',
    )
  ) {
    throw new Error('ReleaseGate returned an invalid runtime snapshot.');
  }

  return snapshot as RuntimeSnapshot;
}
