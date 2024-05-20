# Changelog

All notable changes to this project will be documented in this file. See [standard-version](https://github.com/conventional-changelog/standard-version) for commit guidelines.

## [3.0.0](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/compare/v2.1.0...v3.0.0) (2024-03-28)


### ⚠ BREAKING CHANGES

* - Removed ParseMessage methods from IChatCommunicator interface
- Merged ReceiveMessageAsync() methods into one
- Now both communicators use Message parsing static methods

### Bug Fixes

* reply needs to print to stderr ([8047457](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/8047457edb41af9e2dbfd566a64c2922af6dd52b))
* resolved overloading problem ([60595ae](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/60595ae966c8d11641af0c127bd411f26f6fef52))

## [2.1.0](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/compare/v2.0.0...v2.1.0) (2024-03-27)


### Features

* added makefile that generates a single binary file ([30ee0ba](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/30ee0ba1375dacdbd4839b313fa84208365df8cf))

## 2.0.0 (2024-03-27)


### ⚠ BREAKING CHANGES

* - Changed the way input handling and listening methods start
- ListenForMessagesAsync checks selected protocol and acts on it
- Whole Message handling logic moved to HandleMessageByType() method
- New Message type implemented: Confirm
- Added a function to send Error messages as the FSM in the schema states (yes, I forgot to implement it earlier)
* introduces chat communicator interface to be used by ChatClient, which starts to focus on the chat client itself (handling commands etc)

### Features

* add separate network logic objects ([191874f](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/191874f90e83851d7c7656adff600a4e7a6eb92b))
* added cancellation token to handle waiting for input ([c973bb6](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/c973bb6f8931eb79ddc556b8490fe21dc2d45245))
* added command handler interface ([65c0a77](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/65c0a77ffc51ece9d3249978a4be59a4d59f787e))
* added message parameter checking ([602b2f6](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/602b2f6f6c54ffd50cf0ddb55e0251d52c3aa335))
* implement UdpChatCommunicator class ([55a7efb](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/55a7efb937ecf85d31bd2e36aac7db91472c93ef))
* implement UdpCommunicator in Program.cs ([3b2c18b](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/3b2c18bf234af3a67624ce31c6e62f4cf201c431))
* implement UdpCommunicator into ChatClient logic ([fba46c7](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/fba46c7798559fe1117cdca67a8e5e2a5b153e13))
* message serialization and parsing from UDP in Message.cs ([21d14ee](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/21d14eecd6e0125a7cd6e30eecb8f6e9c293e48a))
* prepare interface for UDP communication ([27771ef](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/27771ef78dc8bbc1a58565e60cafc5017f4ab511))
* rework of messages so its abstract ([1ce30d7](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/1ce30d7fcdfe5ac5f907edba988c602effb90755))
* semaphore to handle just one command at a time ([d9d2e65](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/d9d2e659294fa4253710a2908043736b9128d64b))
* TCP variant complete functionality ([a1c4190](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/a1c4190abcf6a98e3aa7e9e3dc74627892a31a8e))


### Bug Fixes

* allow lowercase messages ([192964b](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/192964b4a6aa52cd8a1e67388e510b1bff6f07e6))
* avoid working with IPv6 in TcpCommunicator ([393596b](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/393596b818dcd84bcff107732ef62e237a33def1))
* cannot auth if not in auth state ([d542199](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/d5421994326491abe73252bb51f0efc4fc78f4c7))
* change client state and lastcommandsent in Auth before sending Auth message ([48c51ea](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/48c51ea57ff026f9e6cb6383f65cf8153b8e703b))
* correct state handling in case of a reply ([124ed2e](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/124ed2ea064074e9db9450923fdcabb17865fe54))
* disconnect right after sending BYE message ([781e7ce](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/781e7cebf59a6274685abab681e3fcb97b698a1f))
* enforce -t and -s arguments to be manually inputed ([aea7509](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/aea750904e04fae119cf477ed4ac464ed885cf4e))
* join message can't be sent unless in open state ([6dfaefc](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/6dfaefc74b561867506a25b3608ec6d709769e3a))
* JoinHandler needs to unlock semaphore if join is illegaly used ([5109710](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/5109710e36397860f3c9ba59986617ff6ecffd9b))
* made program take piped input / redirected input from a file ([daacae5](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/daacae502f3b81e2395f35648e62e18102efd9a6))
* message checks if its format is really correct ([ab4c74c](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/ab4c74c3805f1a9733f81bf4b22a49041f537b6a))
* receiving any server message other than the accepted in the current state leads to error and termination ([df51e69](https://git.fit.vutbr.cz/xjakub41/ipk_proj1/commit/df51e6955ed1b8972f095dbe879f2fbce281ca36))
