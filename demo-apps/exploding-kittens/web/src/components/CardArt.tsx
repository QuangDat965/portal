import type { CSSProperties } from 'react'

export const cardColors: Record<string, string> = {
  defuse: '#a8ce76', skip: '#81c6d8', attack: '#f39c4b', shuffle: '#dca5da',
  see_the_future: '#aaa0e0', favor: '#f1ce6e', nope: '#f08990', exploding_kitten: '#f57558',
}

function Cat({ angry = false, color = '#f9d780' }: { angry?: boolean; color?: string }) {
  return <g stroke="#382a39" strokeWidth="5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M51 139Q28 103 51 76L47 38 82 61Q103 53 127 61L161 36 159 80Q181 115 152 141L154 178Q106 198 54 177Z" fill={color} />
    <path d="m57 53 3 26 18-13M149 52l-17 15 18 11" fill="#f18c86" strokeWidth="3" />
    <path d="M67 160q-39 22-42-3-2-17 14-14" fill="none" stroke={color} strokeWidth="13" />
    <path d="M67 160q-39 22-42-3-2-17 14-14" fill="none" strokeWidth="3" />
    <ellipse cx="103" cy="157" rx="24" ry="23" fill="#fff2cb" stroke="none" />
    {angry ? <><path d="m67 96 17 7m40 0 17-7" /><path d="m77 108 3 3m48 0 3-3" /></> : <><ellipse cx="79" cy="104" rx="4" ry="7" fill="#382a39" stroke="none" /><ellipse cx="131" cy="104" rx="4" ry="7" fill="#382a39" stroke="none" /></>}
    <path d="m99 116 7 6 7-6Z" fill="#e57e83" strokeWidth="3" /><path d="M106 122v6m0-2q-7 10-13 2m13-2q7 10 13 2" fill="none" strokeWidth="3" />
    <path d="m58 115-18-4m19 12-20 2m114-10 18-4m-18 12 20 2" strokeWidth="3" />
    <path d="m70 173 0 13m65-13v13" /><path d="m92 72 3 8m12-9v9m11-7-2 7" strokeWidth="3" />
  </g>
}

export function CardArt({ type = 'exploding_kitten', className = '' }: { type?: string; className?: string }) {
  return <svg className={`card-art ${className}`} viewBox="0 0 210 215" fill="none" aria-hidden="true" style={{ '--art-color': cardColors[type] ?? '#f57558' } as CSSProperties}>
    <ellipse cx="108" cy="187" rx="67" ry="10" fill="#382a39" opacity=".12" />
    {type === 'exploding_kitten' ? <>
      <path d="m105 16 16 22 32-11-1 28 38 6-19 29 25 22-36 13 8 34-38-5-18 38-21-30-36 11 5-37-35-12 25-23-17-30 34 1 7-32Z" fill="#f7cf5d" stroke="#382a39" strokeWidth="3" />
      <g transform="translate(13 14) scale(.85)"><Cat angry color="#ed8a59" /></g>
      <circle cx="142" cy="159" r="24" fill="#382a39" /><path d="M147 136q-7-25 14-25" stroke="#382a39" strokeWidth="5" /><path d="m165 101 3 8 9-3-4 8 7 5-11 1-2 9-5-8-9 1 5-8-4-7 8 3Z" fill="#f6be4e" />
    </> : type === 'defuse' ? <>
      <path d="m28 69 16-8m121-24 9-12m-4 57 19-2" stroke="#f9f1c9" strokeWidth="8" strokeLinecap="round" />
      <Cat color="#f7eac4" /><path d="M64 149q38-21 81 17M68 174q38-21 76-7" stroke="#ee755d" strokeWidth="5" />
      <g transform="translate(113 125) rotate(-25)"><circle cx="-7" cy="37" r="10" fill="#89b9bc" stroke="#382a39" strokeWidth="4" /><circle cx="17" cy="37" r="10" fill="#89b9bc" stroke="#382a39" strokeWidth="4" /><path d="M-2 29 22-5M12 29-13-5" stroke="#382a39" strokeWidth="5" /><path d="M-2 29 22-5M12 29-13-5" stroke="#fff3db" strokeWidth="2" /></g>
    </> : type === 'skip' ? <>
      <path d="M147 30q-24 23 3 44-49 9-42-33Z" fill="#fff0b0" />
      <g transform="translate(2 18)"><Cat color="#fff0cb" /></g><path d="m70 119 14-4m40 0 14 4" stroke="#382a39" strokeWidth="6" strokeLinecap="round" />
      <path d="m163 98 15 1-12 17 16 1m-5-58 16 2-13 18 16 2" stroke="#fff6da" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M45 169q58 38 114 0v22H45Z" fill="#e88783" stroke="#382a39" strokeWidth="4" />
    </> : type === 'attack' ? <>
      <path d="m46 26-15 28 17 3-15 28 42-38-20-2 15-21m105 72-15 28 16 3-12 28 40-39-20-1 13-22" fill="#ffe9a4" stroke="#382a39" strokeWidth="3" />
      <Cat angry color="#ec8157" /><path d="m51 144 23-12 8 26-27 7Z" fill="#e76869" stroke="#382a39" strokeWidth="4" /><path d="m132 144 22-12 8 26-26 7Z" fill="#e76869" stroke="#382a39" strokeWidth="4" />
      <path d="m91 145 13-18-1 16 15-2-14 25 1-18Z" fill="#fff0b0" />
    </> : type === 'see_the_future' ? <>
      <path d="m44 42 4 13 14 2-11 8 2 13-11-8-12 5 4-13-9-10 14 1Zm126 42 4 9 10 1-8 6 2 10-8-5-9 4 2-10-7-7 10 1Z" fill="#fff0b0" />
      <Cat color="#d5b8e6" /><path d="m65 68 38-55 38 55q-40-14-76 0Z" fill="#7764ac" stroke="#382a39" strokeWidth="4" /><path d="m99 38 4 6 7-1-3 6 3 6-8-2-5 5 1-8-5-5 7-1Z" fill="#fff0ae" />
      <circle cx="107" cy="164" r="29" fill="#a0d7d4" stroke="#382a39" strokeWidth="4" /><path d="M88 162q19-22 38 0-19 22-38 0Z" fill="#fff2ce" stroke="#382a39" strokeWidth="3" /><circle cx="107" cy="162" r="7" fill="#6b5399" /><path d="M80 193h55" stroke="#382a39" strokeWidth="7" strokeLinecap="round" />
    </> : type === 'favor' ? <>
      <Cat color="#f4e9cf" /><path d="M49 154q10-24 38-13m73 13q-10-24-38-13" stroke="#382a39" strokeWidth="5" />
      <path d="M73 156q24-26 48-4l24-12-1 32-24-12q-28 20-47-4Z" fill="#7cb9c3" stroke="#382a39" strokeWidth="4" /><circle cx="88" cy="153" r="3" fill="#382a39" /><path d="m112 147 0 19" stroke="#382a39" strokeWidth="3" />
      <path d="M163 39q-14-17-23-5-9 11 18 26 24-14 18-24-5-10-13 3Z" fill="#e87883" />
    </> : type === 'shuffle' ? <>
      <g transform="translate(-14 28) rotate(-12 100 130) scale(.8)"><Cat color="#e8a77a" /></g>
      <g transform="translate(70 23) rotate(15 100 130) scale(.64)"><Cat color="#fff0ce" /></g>
      <path d="M45 43q53-38 108 0m-12-20 15 22-27 5M168 169q-56 37-111 0m13 22-17-24 26-5" stroke="#fff0b0" strokeWidth="7" strokeLinecap="round" strokeLinejoin="round" />
    </> : <><Cat angry color="#f3c5bf" /><circle cx="104" cy="145" r="37" fill="#ea726e" stroke="#382a39" strokeWidth="5" /><path d="m79 169 50-49" stroke="#fff2d4" strokeWidth="9" /></>}
  </svg>
}

export function AvatarArt({ index = 0 }: { index?: number }) {
  return <svg viewBox="0 0 100 100" fill="none" aria-hidden="true">
    <circle cx="50" cy="50" r="48" fill={['#d8afdc', '#9dcbd1', '#e6b878', '#a8c58d', '#e2a4a2'][index % 5]} />
    <g transform="translate(-1 -1) scale(.48)"><Cat angry={index % 3 === 2} color={['#fff0cf', '#e7a16b', '#f7e3ad', '#c7b7dc', '#d0dce0'][index % 5]} /></g>
  </svg>
}
