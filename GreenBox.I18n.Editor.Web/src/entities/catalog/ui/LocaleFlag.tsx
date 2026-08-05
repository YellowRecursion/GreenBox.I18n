import { GlobalOutlined } from '@ant-design/icons'
import AfFlag from 'country-flag-icons/react/3x2/ZA'
import ArFlag from 'country-flag-icons/react/3x2/SA'
import BeFlag from 'country-flag-icons/react/3x2/BY'
import BgFlag from 'country-flag-icons/react/3x2/BG'
import CnFlag from 'country-flag-icons/react/3x2/CN'
import CsFlag from 'country-flag-icons/react/3x2/CZ'
import DaFlag from 'country-flag-icons/react/3x2/DK'
import DeFlag from 'country-flag-icons/react/3x2/DE'
import ElFlag from 'country-flag-icons/react/3x2/GR'
import EnFlag from 'country-flag-icons/react/3x2/US'
import EsFlag from 'country-flag-icons/react/3x2/ES'
import EtFlag from 'country-flag-icons/react/3x2/EE'
import FiFlag from 'country-flag-icons/react/3x2/FI'
import FrFlag from 'country-flag-icons/react/3x2/FR'
import HeFlag from 'country-flag-icons/react/3x2/IL'
import HiFlag from 'country-flag-icons/react/3x2/IN'
import HuFlag from 'country-flag-icons/react/3x2/HU'
import IdFlag from 'country-flag-icons/react/3x2/ID'
import IsFlag from 'country-flag-icons/react/3x2/IS'
import ItFlag from 'country-flag-icons/react/3x2/IT'
import JaFlag from 'country-flag-icons/react/3x2/JP'
import KoFlag from 'country-flag-icons/react/3x2/KR'
import LtFlag from 'country-flag-icons/react/3x2/LT'
import LvFlag from 'country-flag-icons/react/3x2/LV'
import NlFlag from 'country-flag-icons/react/3x2/NL'
import NoFlag from 'country-flag-icons/react/3x2/NO'
import PlFlag from 'country-flag-icons/react/3x2/PL'
import PtFlag from 'country-flag-icons/react/3x2/PT'
import RoFlag from 'country-flag-icons/react/3x2/RO'
import RuFlag from 'country-flag-icons/react/3x2/RU'
import ShFlag from 'country-flag-icons/react/3x2/RS'
import SkFlag from 'country-flag-icons/react/3x2/SK'
import SlFlag from 'country-flag-icons/react/3x2/SI'
import SvFlag from 'country-flag-icons/react/3x2/SE'
import ThFlag from 'country-flag-icons/react/3x2/TH'
import TrFlag from 'country-flag-icons/react/3x2/TR'
import TwFlag from 'country-flag-icons/react/3x2/TW'
import UkFlag from 'country-flag-icons/react/3x2/UA'
import ViFlag from 'country-flag-icons/react/3x2/VN'

const flagsByLanguage = {
  af: AfFlag,
  ar: ArFlag,
  be: BeFlag,
  bg: BgFlag,
  ca: EsFlag,
  cs: CsFlag,
  da: DaFlag,
  de: DeFlag,
  el: ElFlag,
  en: EnFlag,
  es: EsFlag,
  et: EtFlag,
  eu: EsFlag,
  fi: FiFlag,
  fo: DaFlag,
  fr: FrFlag,
  he: HeFlag,
  hi: HiFlag,
  hu: HuFlag,
  id: IdFlag,
  is: IsFlag,
  it: ItFlag,
  ja: JaFlag,
  ko: KoFlag,
  lt: LtFlag,
  lv: LvFlag,
  nb: NoFlag,
  nl: NlFlag,
  nn: NoFlag,
  no: NoFlag,
  pl: PlFlag,
  pt: PtFlag,
  ro: RoFlag,
  ru: RuFlag,
  sh: ShFlag,
  sk: SkFlag,
  sl: SlFlag,
  sr: ShFlag,
  sv: SvFlag,
  th: ThFlag,
  tr: TrFlag,
  uk: UkFlag,
  vi: ViFlag,
  zh: CnFlag,
}

interface LocaleFlagProps {
  culture: string
}

export function LocaleFlag({ culture }: LocaleFlagProps) {
  const Flag = getFlag(culture)
  if (!Flag) {
    return <GlobalOutlined aria-hidden />
  }

  return (
    <Flag
      aria-hidden
      style={{
        display: 'block',
        width: '1em',
        height: '0.75em',
        borderRadius: 2,
        objectFit: 'cover',
      }}
    />
  )
}

function getFlag(culture: string) {
  const normalizedCulture = culture.trim().replaceAll('_', '-').toLowerCase()
  const cultureParts = normalizedCulture.split('-')
  if (cultureParts[0] === 'zh' &&
      cultureParts.some((part) => part === 'hant' || part === 'tw' || part === 'hk' || part === 'mo')) {
    return TwFlag
  }

  return flagsByLanguage[cultureParts[0] as keyof typeof flagsByLanguage]
}
