import { Unity, useUnityContext } from "react-unity-webgl";

function LoadUnity() {
    const { unityProvider } = useUnityContext({
        loaderUrl: "webgl/Build/WEB.loader.js",
        dataUrl: "webgl/Build/WEB.data",
        frameworkUrl: "webgl/Build/WEB.framework.js",
        codeUrl: "webgl/Build/WEB.wasm",
        });

  return (
    <Unity unityProvider={unityProvider} style={{ width: 800, height: 600 }} />
  );
}
export default LoadUnity;
