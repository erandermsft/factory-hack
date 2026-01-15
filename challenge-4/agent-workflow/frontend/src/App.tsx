import { useMemo, useState } from 'react'
import aspireLogo from '/Aspire.png'
import './App.css'

import { AlarmForm, type AnalyzeMachinePayload } from './components/AlarmForm'
import { AgentIllustration, type AgentNode } from './components/AgentIllustration'

function App() {
  const apiBaseUrl = import.meta.env.VITE_API_URL as string | undefined
  const analyzeMachineUrl = apiBaseUrl
    ? new URL('/api/analyze_machine', apiBaseUrl).toString()
    : '/api/analyze_machine'

  const agents = useMemo<AgentNode[]>(
    () => [
      {
        id: 'anomaly',
        name: 'Anomaly Classification Agent',
        description: 'Determines whether the alarm indicates an anomaly and classifies severity.',
      },
      {
        id: 'diagnosis',
        name: 'Fault Diagnosis Agent',
        description: 'Analyzes symptoms and proposes likely root causes and next checks.',
      },
      {
        id: 'planner',
        name: 'Repair Planner Agent',
        description: 'Drafts a repair plan, parts list, and recommended technician actions.',
      },
    ],
    [],
  )

  const [submittedPayload, setSubmittedPayload] = useState<AnalyzeMachinePayload | null>(null)
  const [runState, setRunState] = useState<'idle' | 'running' | 'completed'>('idle')
  const activeIndex: number | null = null
  const [apiResponse, setApiResponse] = useState<unknown>(null)
  const [apiError, setApiError] = useState<string | null>(null)

  const callAnalyzeMachine = async (payload: AnalyzeMachinePayload) => {
    setSubmittedPayload(payload)
    setRunState('running')
    setApiResponse(null)
    setApiError(null)

    try {
      const response = await fetch(analyzeMachineUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })

      const contentType = response.headers.get('content-type') || ''
      const body = contentType.includes('application/json')
        ? await response.json()
        : await response.text()

      if (!response.ok) {
        const message =
          typeof body === 'string'
            ? body
            : (body as { error?: string; detail?: string }).error ||
              (body as { error?: string; detail?: string }).detail ||
              `Request failed with ${response.status}`
        throw new Error(message)
      }

      setApiResponse(body)
      setRunState('completed')
    } catch (err) {
      setApiError(err instanceof Error ? err.message : 'Request failed')
      setRunState('idle')
    }
  }

  const reset = () => {
    setRunState('idle')
    setSubmittedPayload(null)
    setApiResponse(null)
    setApiError(null)
  }

  return (
    <div className="app-container">
      <header className="app-header">
        <a 
          href="https://aspire.dev" 
          target="_blank" 
          rel="noopener noreferrer"
          aria-label="Visit Aspire website (opens in new tab)"
          className="logo-link"
        >
          <img src={aspireLogo} className="logo" alt="Aspire logo" />
        </a>
        <h1 className="app-title">Factory Agent Workflow</h1>
        <p className="app-subtitle">
          Define an alarm, then watch agents process it (mocked).
        </p>
      </header>

      <main className="main-content">
        <section className="workflow-layout" aria-label="Alarm submission and agent workflow">
          <div className="card">
            <AlarmForm disabled={runState === 'running'} onSubmit={callAnalyzeMachine} />

            {apiError && (
              <div className="error-message" role="alert" aria-live="polite">
                <span>{apiError}</span>
              </div>
            )}

            <div className="card-footer">
              <button
                type="button"
                className="secondary-button"
                onClick={reset}
                disabled={runState === 'running'}
              >
                Reset
              </button>
              <div className="muted">
                {runState === 'running'
                  ? 'Calling API…'
                  : apiBaseUrl
                    ? `Using VITE_API_URL: ${apiBaseUrl}`
                    : 'Using relative /api (Vite proxy).'}
              </div>
            </div>
          </div>

          <div className="card">
            <AgentIllustration agents={agents} activeIndex={activeIndex} runState={runState} />

            <div className="submitted-preview">
              <div className="section-header">
                <h2 className="section-title">Request/response</h2>
              </div>
              <div className="request-response-grid">
                <div>
                  <div className="muted">Request</div>
                  {submittedPayload ? (
                    <pre className="code-block" aria-label="Request JSON">
                      {JSON.stringify(submittedPayload, null, 2)}
                    </pre>
                  ) : (
                    <div className="muted">Submit to see request payload.</div>
                  )}
                </div>
                <div>
                  <div className="muted">Response</div>
                  {apiResponse != null ? (
                    <pre className="code-block" aria-label="Response">
                      {typeof apiResponse === 'string'
                        ? apiResponse
                        : JSON.stringify(apiResponse, null, 2)}
                    </pre>
                  ) : (
                    <div className="muted">Awaiting response.</div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </section>
      </main>

      <footer className="app-footer">
        <nav aria-label="Footer navigation">
          <a href="https://aspire.dev" target="_blank" rel="noopener noreferrer">
            Built on Aspire + Vite<span className="visually-hidden"> (opens in new tab)</span>
          </a>
          <a 
            href="https://github.com/dotnet/aspire" 
            target="_blank" 
            rel="noopener noreferrer"
            className="github-link"
            aria-label="View Aspire on GitHub (opens in new tab)"
          >
            <img src="/github.svg" alt="" width="24" height="24" aria-hidden="true" />
            <span className="visually-hidden">GitHub</span>
          </a>
        </nav>
      </footer>
    </div>
  )
}

export default App
