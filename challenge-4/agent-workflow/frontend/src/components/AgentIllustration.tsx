export type AgentStatus = 'pending' | 'running' | 'done' | 'idle'

export interface AgentNode {
  id: string
  name: string
  description: string
}

function statusLabel(status: AgentStatus) {
  switch (status) {
    case 'running':
      return 'Working'
    case 'done':
      return 'Done'
    case 'pending':
      return 'Queued'
    default:
      return 'Idle'
  }
}

export function AgentIllustration(props: {
  agents: AgentNode[]
  activeIndex: number | null
  runState: 'idle' | 'running' | 'completed'
}) {
  const { agents, activeIndex, runState } = props

  const getStatus = (index: number): AgentStatus => {
    if (runState === 'idle') return 'idle'
    if (runState === 'completed') return 'done'
    if (activeIndex == null) return 'pending'
    if (index < activeIndex) return 'done'
    if (index === activeIndex) return 'running'
    return 'pending'
  }

  const activeAgent =
    activeIndex == null ? null : agents[Math.min(activeIndex, agents.length - 1)]

  return (
    <div className="agent-illustration" aria-label="Agents in the workflow">
      <div className="section-header">
        <div>
          <h2 className="section-title">Agents</h2>
          <p className="muted">
            {runState === 'running' && activeAgent
              ? `Currently working: ${activeAgent.name}`
              : runState === 'completed'
                ? 'Run completed'
                : 'Waiting for an alarm'}
          </p>
        </div>
        <div className={`run-pill run-pill--${runState}`} aria-label={`Run state: ${runState}`}>
          <span className="run-pill__dot" aria-hidden="true" />
          <span className="run-pill__text">{runState}</span>
        </div>
      </div>

      <ol className="agent-list">
        {agents.map((agent, index) => {
          const status = getStatus(index)
          return (
            <li
              key={agent.id}
              className={`agent-node agent-node--${status}`}
              aria-current={status === 'running' ? 'step' : undefined}
            >
              <div className="agent-node__rail" aria-hidden="true">
                <div className="agent-node__dot" />
                {index !== agents.length - 1 && <div className="agent-node__line" />}
              </div>
              <div className="agent-node__card">
                <div className="agent-node__top">
                  <div className="agent-node__title">{agent.name}</div>
                  <span className={`badge badge--${status}`}>
                    {statusLabel(status)}
                  </span>
                </div>
                <div className="agent-node__desc">{agent.description}</div>
              </div>
            </li>
          )
        })}
      </ol>

      <div className="agent-legend" aria-label="Legend">
        <div className="legend-item">
          <span className="legend-swatch legend-swatch--pending" aria-hidden="true" />
          Queued
        </div>
        <div className="legend-item">
          <span className="legend-swatch legend-swatch--running" aria-hidden="true" />
          Working
        </div>
        <div className="legend-item">
          <span className="legend-swatch legend-swatch--done" aria-hidden="true" />
          Done
        </div>
      </div>
    </div>
  )
}
