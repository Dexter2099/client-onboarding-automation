import { useState } from 'react';
import type { RefObject } from 'react';
import { Check, ChevronRight, FileText, Play, Sparkles } from 'lucide-react';

interface Props {
  mainHeading: RefObject<HTMLHeadingElement | null>;
  onStart: () => void;
}

export function Instructions({ mainHeading, onStart }: Props) {
  // Playback is view-local and resets naturally when the visitor leaves Instructions.
  const [walkthroughPlaying, setWalkthroughPlaying] = useState(false);
  return (
    <article className="welcome-card">
      <h2 ref={mainHeading} tabIndex={-1}>
        Welcome to your new client onboarding!
      </h2>
      <p>Let's get your business set up with Harbour Bookkeeping.</p>
      <p>
        We've prepared a fictional client information pack. Watch AI turn it into draft answers,
        check the sources, then submit your information for review.
      </p>
      <div className="walkthrough-video">
        {walkthroughPlaying ? (
          <video
            controls
            autoPlay
            muted
            playsInline
            poster="/onboardflow-local-ai-20261025.webp"
            aria-label="Recorded walkthrough using local AI"
          >
            <source src="/onboardflow-local-ai-20261025.mp4" type="video/mp4" />
            <source src="/onboardflow-local-ai-20261025.webm" type="video/webm" />
            Your browser does not support this preview recording.
          </video>
        ) : (
          <button
            type="button"
            className="video-cover"
            aria-label="Play walkthrough"
            onClick={() => setWalkthroughPlaying(true)}
          >
            <img
              src="/onboardflow-local-ai-20261025.webp"
              alt="Source-linked answers in the recorded local walkthrough"
            />
            <span className="play-icon">
              <Play size={30} fill="currentColor" />
            </span>
          </button>
        )}
        <span>Recorded local AI · Ollama</span>
      </div>
      <p className="intro-platform">Complete your request at your own pace.</p>
      <div className="instruction-row">
        <FileText size={25} />
        <div>
          <strong>Your answers are saved.</strong>
          <p>Leave and return to this preview without starting again.</p>
        </div>
      </div>
      <div className="instruction-row">
        <Sparkles size={25} />
        <div>
          <strong>AI fills from the sample documents.</strong>
          <p>Every AI draft links back to its supporting passage.</p>
        </div>
      </div>
      <div className="instruction-row">
        <Check size={25} />
        <div>
          <strong>You review and submit.</strong>
          <p>AI leaves missing information and confirmations for you.</p>
        </div>
      </div>
      <div className="instruction-legend">
        <span>
          <i className="legend-dot draft" />
          Draft
        </span>
        <span>
          <i className="legend-dot submitted" />
          Awaiting review
        </span>
        <span>
          <i className="legend-dot missing" />
          Needs information
        </span>
      </div>
      <button type="button" className="primary start-button" onClick={onStart}>
        LET'S GET STARTED
        <ChevronRight size={15} />
      </button>
    </article>
  );
}
