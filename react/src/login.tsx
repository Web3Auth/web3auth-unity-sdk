import React from "react";
import LoadUnity from "./LoadUnity";

import "./App.css";
import TorusSdk, { TorusLoginResponse } from "@toruslabs/customauth";
import ReactJsonView from "react-json-view";
import {
  verifierMap,
  GOOGLE,
  GITHUB,
  TWITTER,
  APPLE,
  AUTH_DOMAIN,
  EMAIL_PASSWORD,
  HOSTED_EMAIL_PASSWORDLESS,
  HOSTED_SMS_PASSWORDLESS,
  LINE,
  LINKEDIN,
  WEIBO,
  COGNITO_AUTH_DOMAIN,
  COGNITO,
  REDDIT,
} from "./constants";

interface IState {
  selectedVerifier: string;
  torusdirectsdk: TorusSdk | null;
  loginResponse?: TorusLoginResponse | null;
}

interface IProps {}

class PopupMode extends React.Component<IProps, IState> {
  constructor(props: IProps) {
    super(props);
    this.state = {
      selectedVerifier: GOOGLE,
      torusdirectsdk: null,
      loginResponse: null,
    };
  }

  componentDidMount = async () => {
    try {
      const torusdirectsdk = new TorusSdk({
        baseUrl: `${window.location.origin}/serviceworker`,
        enableLogging: true,
        network: "testnet", // details for test net
      });

      await torusdirectsdk.init({ skipSw: false });

      this.setState({ torusdirectsdk });
    } catch (error) {
      console.error(error, "mounted caught");
    }
  };

  login = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const { selectedVerifier, torusdirectsdk } = this.state;
    // console.log(hash, queryParameters);
    try {
      const jwtParams = this._loginToConnectionMap()[selectedVerifier] || {};
      const { typeOfLogin, clientId, verifier } = verifierMap[selectedVerifier];
      const loginDetails = await torusdirectsdk?.triggerLogin({
        typeOfLogin,
        verifier,
        clientId,
        jwtParams,
      });
      this.setState({ loginResponse: loginDetails });
    } catch (error) {
      console.error(error, "login caught");
    }
  };

  _loginToConnectionMap = (): Record<string, any> => {
    return {
      [EMAIL_PASSWORD]: { domain: AUTH_DOMAIN },
      [HOSTED_EMAIL_PASSWORDLESS]: {
        domain: AUTH_DOMAIN,
        verifierIdField: "name",
        connection: "",
        isVerifierIdCaseSensitive: false,
      },
      [HOSTED_SMS_PASSWORDLESS]: { domain: AUTH_DOMAIN, verifierIdField: "name", connection: "" },
      [APPLE]: { domain: AUTH_DOMAIN },
      [GITHUB]: { domain: AUTH_DOMAIN },
      [LINKEDIN]: { domain: AUTH_DOMAIN },
      [TWITTER]: { domain: AUTH_DOMAIN },
      [WEIBO]: { domain: AUTH_DOMAIN },
      [LINE]: { domain: AUTH_DOMAIN },
      [COGNITO]: { domain: COGNITO_AUTH_DOMAIN, identity_provider: "Google", response_type: "token", user_info_endpoint: "userInfo" },
      [REDDIT]: { domain: AUTH_DOMAIN, connection: "Reddit", verifierIdField: "name", isVerifierIdCaseSensitive: false },
    };
  };

  render() {
    const { selectedVerifier, loginResponse } = this.state;
    localStorage.setItem('token', JSON.stringify(loginResponse));
    return (
      <div className="App">
        <form onSubmit={this.login}>
          <div>
            <span style={{ marginRight: "10px" }}>Verifier:</span>
            <select value={selectedVerifier} onChange={(e) => this.setState({ selectedVerifier: e.target.value })}>
              {Object.keys(verifierMap).map((login) => (
                <option value={login} key={login.toString()}>
                  {verifierMap[login].name}
                </option>
              ))}
            </select>
          </div>
          <div style={{ marginTop: "20px" }}>
            <button>Login with Torus</button>
          </div>
        </form>
        {loginResponse && (
          <div>
            <LoadUnity />
          </div>
        )}
      </div>
    );
  }
}

export default PopupMode;
