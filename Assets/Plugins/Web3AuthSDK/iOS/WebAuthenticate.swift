import AuthenticationServices
import SafariServices
import UIKit

@objc public class WebAuthenticate: NSObject {
    public static let instance = WebAuthenticate();
    
    @objc public func call(_ url: String,_ redirectUri: String) {
        let authSession = ASWebAuthenticationSession(
            url: URL(string: url)!, callbackURLScheme: redirectUri) { callbackURL, authError in
                
                print("hello world");
                let unity = UnityFramework.getInstance();
                unity?.sendMessageToGO(withName: "Web3Auth", functionName: "onDeepLinkActivated", message: callbackURL?.absoluteString);
        }
        
        if #available(iOS 13.0, *) {
            authSession.presentationContextProvider = self
            authSession.prefersEphemeralWebBrowserSession = true
        }
        
        authSession.start();
    }
    
    @objc public static func launch(_ url: String,_ redirectUri: String) {
        instance.call(url, redirectUri);
    }
}


@available(iOS 12.0, *)
extension WebAuthenticate: ASWebAuthenticationPresentationContextProviding {
    public func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        let window = UIApplication.shared.windows.first { $0.isKeyWindow }
        return window ?? ASPresentationAnchor()
    }
}
