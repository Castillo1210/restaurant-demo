/// <reference types="vite/client" />
// CSS module declarations
declare module '*.css' {
  const content: Record<string, string>;
  export default content;
}

// Asset declarations
declare module '*.svg' {
  const content: string;
  export default content;
}

declare module '*.png' {
  const content: string;
  export default content;
}

declare module '*.jpg' {
  const content: string;
  export default content;
}
