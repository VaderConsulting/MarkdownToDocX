# Enterprise diagram samples

Native C# renderer. Samples 03, 06, 07 and 12.

## Cross-layer web shop

```enterprise
enterprise
  title Web shop - application and technology view
  direction TB

  group dc "Primary data centre"

  application-component webShop  "Web Shop"
  application-interface shopUi   "Shop Web UI"
  application-function  checkout "Checkout" in webShop
  application-service   ordering "Online Ordering"
  data-object           order    "Order"

  technology-service    hosting  "Java Application Hosting"
  technology-service    dbSvc    "Database Service"
  node                  appNode  "App Server Node" in dc
  system-software       jvm      "Java VM" in appNode
  system-software       pg       "PostgreSQL" in dc
  device                lb       "Load Balancer" in dc
  communication-network lan      "DC LAN" in dc
  artifact              war      "shop.war" in appNode
  artifact              orderTbl "orders table"

  webShop -composition-> shopUi
  webShop -assignment-> checkout
  checkout -realization-> ordering
  shopUi -assignment-> ordering
  checkout -access(write)-> order
  appNode -composition-> jvm
  appNode -assignment-> war
  war -realization-> webShop
  orderTbl -realization-> order
  jvm -realization-> hosting
  pg -realization-> dbSvc
  hosting -serving-> webShop
  dbSvc -serving-> webShop
  pg -assignment-> orderTbl
  lb -serving-> appNode "HTTPS"
  lan -association-> lb
  lan -association-> appNode
```

## Business layer

```enterprise
enterprise
  title Business layer - actors, behaviour and products
  direction TB

  business-actor          clerk       "Order Clerk"
  business-role           seller      "Seller"
  business-collaboration  salesTeam   "Sales Team"
  business-interface      shopFront   "Shop Front"
  business-function       fulfil      "Fulfil Orders"
  business-interaction    handoff     "Order Handoff"
  business-process        takeOrder   "Take Order"
  business-event          orderTaken  "Order Taken"
  business-service        selling     "Selling"
  business-object         orderForm   "Order Form"
  contract                terms       "Terms of Sale"
  representation          paperOrder  "Paper Order"
  product                 shopOffer   "Shop Product"

  clerk -assignment-> seller
  salesTeam -aggregation-> seller
  seller -assignment-> takeOrder
  takeOrder -realization-> selling
  fulfil -realization-> selling
  shopFront -assignment-> selling
  selling -serving-> seller
  takeOrder -triggering-> orderTaken
  orderTaken -triggering-> handoff
  takeOrder -access(write)-> orderForm
  terms -specialization-> orderForm
  paperOrder -realization-> orderForm
  shopOffer -aggregation-> selling
```

## Motivation

```enterprise
enterprise
  title Motivation - goals, principles and requirements
  direction TB

  stakeholder   board     "Board"
  driver        growth    "Revenue Growth"
  assessment    gapReview "Growth Gap"
  goal          raiseRev  "Raise Revenue"
  outcome       moreSales "More Sales"
  principle     customer  "Customer First"
  requirement   mustTrack "Must Track Orders"
  constraint    noCash    "No Cash Handling"
  meaning       orderSense "What an order means"
  value         trust     "Customer Trust"

  board -influence(+)-> growth
  growth -association-> gapReview
  gapReview -influence-> raiseRev
  customer -realization-> raiseRev
  mustTrack -realization-> raiseRev
  noCash -specialization-> mustTrack
  moreSales -association-> raiseRev
  orderSense -association-> trust
  board -association-> trust
```

## Motivation to Technology

```enterprise
enterprise
  title Motivation to Technology view
  direction TB

  stakeholder        board      "Board"
  goal               raiseRev   "Raise Revenue"
  capability         sellingCap "Selling Capability"
  business-process   takeOrder  "Take Order"
  business-service   selling    "Selling Service"
  application-component webShop "Web Shop"
  application-service  ordering "Online Ordering"
  node               appNode    "App Server Node"
  technology-service hosting    "Hosting"

  board -influence-> raiseRev
  raiseRev -association-> sellingCap
  sellingCap -association-> takeOrder
  takeOrder -realization-> selling
  selling -serving-> webShop
  webShop -assignment-> ordering
  ordering -serving-> hosting
  appNode -realization-> hosting
```

